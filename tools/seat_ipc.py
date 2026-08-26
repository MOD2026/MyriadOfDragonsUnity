"""Direct CC<->VS message channel over loopback TCP, replacing mailbox polling.

WHY THIS EXISTS
    Seats currently coordinate by appending to tools/seat_mailbox.md and re-reading it.
    That works, but every exchange costs a poll, and a reply is only noticed when someone
    happens to look. The AppContainer named-pipe route is unavailable (verified dead), but
    loopback TCP was measured working across the boundary on 2026-08-26: CC curled a
    listener started from VS's unpackaged process and got HTTP 200, and VS's own probe log
    independently recorded the inbound connection. So loopback is the channel.

DESIGN RULES, each one load-bearing:

  1. THE MAILBOX REMAINS THE SYSTEM OF RECORD. Every message sent through here is ALSO
     appended to tools/seat_mailbox.md. The socket is a notification path, not a store.
     This conversation gets compressed and sessions die; a message that existed only in a
     broker's memory would vanish with it, which is exactly the failure the project's own
     "the conversation is never the system of record" rule exists to prevent.

  2. IT DEGRADES TO THE MAILBOX, NEVER FAILS THE SEND. If no broker is listening, `send`
     still writes the mailbox and exits 0, reporting that it fell back. A coordination
     channel that loses messages when it is down is worse than no channel, because the
     sender believes it was delivered.

  3. LOOPBACK ONLY, AND ENFORCED. The listener binds 127.0.0.1 and additionally refuses
     any peer whose address is not 127.0.0.1. Binding alone is the usual protection; the
     explicit check is there because this carries project coordination traffic and the
     cost of the second line is nothing.

  4. NO SPOOL FILES UNDER Assets/. An untracked file in a compiled source folder broke the
     build for every room tonight. Queues live in memory here; durability is the mailbox.

USAGE
    python tools/seat_ipc.py serve                  # start the broker (background it)
    python tools/seat_ipc.py send VS "text"         # deliver to VS, mailbox-append always
    python tools/seat_ipc.py recv VS --timeout 60   # block for a message addressed to VS
    python tools/seat_ipc.py ping                   # is a broker up?
"""

import argparse
import datetime
import json
import os
import socket
import socketserver
import sys
import threading
from collections import defaultdict, deque

HOST = "127.0.0.1"
PORT = 45677
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAILBOX = os.path.join(REPO, "tools", "seat_mailbox.md")

_queues = defaultdict(deque)
_lock = threading.Lock()
_waiters = defaultdict(threading.Event)


def _stamp():
    return datetime.datetime.now().strftime("%Y-%m-%dT%H:%M:%S")


def append_to_mailbox(sender, recipient, text):
    """The durable half. Written before any socket work, so a crash mid-send still leaves
    the message where the other seat will find it."""
    entry = "\n\n**[{} -> {}] {}**\n".format(sender, recipient, text.strip())
    with open(MAILBOX, "a", encoding="utf-8") as handle:
        handle.write(entry)


class _Handler(socketserver.StreamRequestHandler):
    def handle(self):
        # Rule 3: bind is not the only guard.
        if self.client_address[0] != HOST:
            return

        raw = self.rfile.readline().decode("utf-8", "replace").strip()
        if not raw:
            return
        try:
            msg = json.loads(raw)
        except ValueError:
            self.wfile.write(b'{"ok":false,"error":"bad json"}\n')
            return

        kind = msg.get("kind")

        if kind == "ping":
            self.wfile.write(b'{"ok":true,"pong":true}\n')
            return

        if kind == "send":
            to = msg.get("to", "")
            with _lock:
                _queues[to].append(msg)
                _waiters[to].set()
            self.wfile.write(b'{"ok":true,"queued":true}\n')
            return

        if kind == "recv":
            who = msg.get("as", "")
            timeout = float(msg.get("timeout", 30))
            deadline = threading.Event()
            timer = threading.Timer(timeout, deadline.set)
            timer.daemon = True
            timer.start()
            try:
                while True:
                    with _lock:
                        if _queues[who]:
                            out = _queues[who].popleft()
                            if not _queues[who]:
                                _waiters[who].clear()
                            self.wfile.write((json.dumps({"ok": True, "message": out}) + "\n").encode())
                            return
                        _waiters[who].clear()
                    if deadline.is_set():
                        self.wfile.write(b'{"ok":true,"message":null}\n')
                        return
                    _waiters[who].wait(0.5)
            finally:
                timer.cancel()

        self.wfile.write(b'{"ok":false,"error":"unknown kind"}\n')


class _Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


def _talk(payload, timeout=5.0):
    """One request/response. Returns None when no broker is listening - the caller decides
    what that means rather than this raising."""
    try:
        with socket.create_connection((HOST, PORT), timeout=timeout) as sock:
            sock.sendall((json.dumps(payload) + "\n").encode())
            sock.settimeout(timeout)
            data = b""
            while not data.endswith(b"\n"):
                chunk = sock.recv(65536)
                if not chunk:
                    break
                data += chunk
            return json.loads(data.decode("utf-8", "replace") or "{}")
    except (OSError, ValueError):
        return None


def cmd_serve(_args):
    server = _Server((HOST, PORT), _Handler)
    print("SEAT IPC BROKER listening on {}:{} at {}".format(HOST, PORT, _stamp()), flush=True)
    server.serve_forever()


def cmd_send(args):
    # Rule 1 and 2: durable write FIRST, socket second.
    append_to_mailbox(args.sender, args.to, args.text)

    reply = _talk({
        "kind": "send", "to": args.to, "from": args.sender,
        "text": args.text, "at": _stamp(),
    })

    if reply and reply.get("ok"):
        print("DELIVERED to {} via broker, and appended to the mailbox.".format(args.to))
    else:
        print("NO BROKER - appended to the mailbox only. The message is not lost; "
              "the other seat will see it on their next mailbox read.")
    return 0


def cmd_recv(args):
    reply = _talk({"kind": "recv", "as": args.who, "timeout": args.timeout},
                  timeout=args.timeout + 5)
    if reply is None:
        print("NO BROKER - nothing to receive. Read tools/seat_mailbox.md instead.")
        return 0
    msg = reply.get("message")
    if not msg:
        print("no message within {}s".format(args.timeout))
        return 0
    print("[{} -> {}] at {}\n{}".format(msg.get("from"), msg.get("to"), msg.get("at"), msg.get("text")))
    return 0


def cmd_ping(_args):
    reply = _talk({"kind": "ping"}, timeout=2.0)
    print("BROKER UP" if reply and reply.get("ok") else "BROKER DOWN")
    return 0 if reply else 1


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("serve").set_defaults(func=cmd_serve)

    p_send = sub.add_parser("send")
    p_send.add_argument("to")
    p_send.add_argument("text")
    p_send.add_argument("--sender", default="VS")
    p_send.set_defaults(func=cmd_send)

    p_recv = sub.add_parser("recv")
    p_recv.add_argument("who")
    p_recv.add_argument("--timeout", type=float, default=30.0)
    p_recv.set_defaults(func=cmd_recv)

    sub.add_parser("ping").set_defaults(func=cmd_ping)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
