using System;
using System.Collections.Generic;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>Which visible-action limit a screen is measured against (UI Verification Gate v1
    /// section 2). Top-level is Home and the persistent shell; Secondary is every pushed screen;
    /// Overlay is a modal drawn on top of one.</summary>
    public enum UiSurfaceKind
    {
        TopLevel,
        Secondary,
        Overlay,
    }

    /// <summary>One screen the harness knows how to build. `Build` receives a host-factory so the
    /// CALLER owns spawn tracking and teardown - the registry never creates objects it cannot
    /// clean up, and the contact sheet and the validator therefore tear down identically.</summary>
    public sealed class UiScreenEntry
    {
        public readonly string Name;
        public readonly UiSurfaceKind Surface;
        public readonly Func<Func<string, GameObject>, UiNavigationProbe, GameObject> Build;

        public UiScreenEntry(string name, UiSurfaceKind surface,
            Func<Func<string, GameObject>, UiNavigationProbe, GameObject> build)
        {
            Name = name;
            Surface = surface;
            Build = build;
        }
    }

    /// <summary>
    /// THE single list of every capturable screen, shared by the contact-sheet generator and the
    /// UI validation run.
    ///
    /// It is one list on purpose. Two lists drift: a screen added to the capture harness but not
    /// the validator would be reviewed by eye and never gate-checked, and the gate would still
    /// report a clean pass - which is precisely the "green suite proves nothing visual" failure
    /// the gate exists to stop. The validator's navigation graph is also derived from this same
    /// traversal, so the graph cannot describe a set of screens the capture never rendered.
    ///
    /// Navigation callbacks are RECORDING probes rather than null. A null callback is a
    /// destination that does not exist, which is why the navigation graph had no edges - not
    /// `AddListener`, as first reported. Capture passes a throwaway probe and ignores it; the
    /// crawler reads it.
    ///
    /// SURFACE CLASSIFICATION IS A JUDGEMENT CALL AND IS NOT OWNER-CONFIRMED. Only Home is marked
    /// TopLevel; everything else is Secondary. Nothing is marked Overlay yet, though
    /// EmpireBuildingDetail and PackOpenOverlay are arguable modals whose limit would be 4 rather
    /// than 10. Reclassifying a screen TIGHTENS its limit and can fail a screen that passes today,
    /// so those stay Secondary until the owner rules - guessing here would manufacture failures
    /// with no design behind them.
    /// </summary>
    public static class UiScreenRegistry
    {
        public const int TopLevelActionLimit = 8;
        public const int SecondaryActionLimit = 10;
        public const int OverlayActionLimit = 4;

        public static int ActionLimitFor(UiSurfaceKind kind)
        {
            switch (kind)
            {
                case UiSurfaceKind.TopLevel: return TopLevelActionLimit;
                case UiSurfaceKind.Overlay: return OverlayActionLimit;
                default: return SecondaryActionLimit;
            }
        }

        public static readonly IReadOnlyList<UiScreenEntry> Screens = new List<UiScreenEntry>
        {
            new UiScreenEntry("Home", UiSurfaceKind.TopLevel, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Home");
                var home = host.AddComponent<HomePagePresenter>();
                home.BuildHomePageUIForTests();
                return home.HomeCanvasObjectForTests;
            }),
            new UiScreenEntry("CampaignMap", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_CampaignMap");
                var map = host.AddComponent<CampaignMapPresenter>();
                map.Initialize(onBackToHome: probe.On("onBackToHome"),
                    onLaunchBattle: probe.OnLaunch<CampaignStageData>("onLaunchBattle"));
                return GameObject.Find("CampaignMapCanvas");
            }),
            new UiScreenEntry("Shop", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Shop");
                var shop = host.AddComponent<ShopPresenter>();
                shop.Initialize(SaveSystem.CurrentProfile, onBackToHome: probe.On("onBackToHome"));
                return GameObject.Find("ShopCanvas");
            }),
            new UiScreenEntry("DeckBuilder", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_DeckBuilder");
                var deck = host.AddComponent<DeckBuilderPresenter>();
                deck.Initialize(onBackToHome: probe.On("onBackToHome"));
                return GameObject.Find("DeckBuilderCanvas");
            }),
            new UiScreenEntry("Collection", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Collection");
                var col = host.AddComponent<CollectionPresenter>();
                col.Initialize(onBackToHome: probe.On("onBackToHome"), onOpenDeckBuilder: probe.On("onOpenDeckBuilder"));
                return col.CanvasObjectForTests;
            }),
            new UiScreenEntry("Empire", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Empire");
                var empire = host.AddComponent<EmpirePresenter>();
                empire.Initialize(onBackToHome: probe.On("onBackToHome"));
                return empire.CanvasObjectForTests;
            }),
            new UiScreenEntry("Avatar", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Avatar");
                var avatar = host.AddComponent<AvatarPresenter>();
                avatar.Initialize(onBackToHome: probe.On("onBackToHome"));
                return avatar.CanvasObjectForTests;
            }),
            new UiScreenEntry("Settings", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Settings");
                var settings = host.AddComponent<SettingsPresenter>();
                settings.Initialize(onBackToHome: probe.On("onBackToHome"));
                return settings.CanvasObjectForTests;
            }),
            new UiScreenEntry("EmpireBuildingDetail", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_EmpireBuildingDetail");
                var detail = host.AddComponent<EmpireBuildingDetailPresenter>();
                detail.Initialize(EmpireBuildingKind.Castle, onClose: probe.On("onClose"));
                return GameObject.Find(EmpireBuildingDetailPresenter.CanvasName);
            }),
            new UiScreenEntry("EmpireExpedition", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_EmpireExpedition");
                var exp = host.AddComponent<EmpireExpeditionPresenter>();
                exp.Initialize(onBack: probe.On("onBack"), guildBonusQuery: UnavailableGuildExpeditionBonusQuery.Instance);
                return GameObject.Find(EmpireExpeditionPresenter.CanvasName);
            }),
            new UiScreenEntry("GuildHallEntry", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_GuildHallEntry");
                var gh = host.AddComponent<GuildHallEntryPresenter>();
                gh.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(GuildHallEntryPresenter.CanvasName);
            }),
            new UiScreenEntry("GuildExpedition", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_GuildExpedition");
                var ge = host.AddComponent<GuildExpeditionPresenter>();
                ge.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(GuildExpeditionPresenter.CanvasName);
            }),
            new UiScreenEntry("TacticalPuzzle", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_TacticalPuzzle");
                var tp = host.AddComponent<TacticalPuzzlePresenter>();
                tp.Initialize(TacticalPuzzleLibrary.AvailablePuzzles(), onExit: probe.On("onExit"));
                return GameObject.Find(TacticalPuzzlePresenter.CanvasName);
            }),
            // Battle shell (GameBootstrap) — one of the three zero-chrome screens. Built via the
            // real Initialize path so the contact sheet shows the same canvas a player would see
            // at Formation, not a stub. Canvas is named "Canvas" (battle's historical name).
            new UiScreenEntry("GameBootstrap", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_GameBootstrap");
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.Initialize();
                return GameObject.Find("Canvas");
            }),
            new UiScreenEntry("BattlePass", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_BattlePass");
                var bp = host.AddComponent<BattlePassPresenter>();
                bp.Initialize(onBackToHome: probe.On("onBackToHome"));
                return GameObject.Find(BattlePassPresenter.CanvasName);
            }),
            new UiScreenEntry("DailyLoginQuests", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_DailyLoginQuests");
                var dl = host.AddComponent<DailyLoginQuestsPresenter>();
                dl.Initialize(onBackToHome: probe.On("onBackToHome"));
                return GameObject.Find(DailyLoginQuestsPresenter.CanvasName);
            }),
            new UiScreenEntry("MailInbox", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_MailInbox");
                var mail = host.AddComponent<MailInboxPresenter>();
                mail.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(MailInboxPresenter.CanvasName);
            }),
            new UiScreenEntry("Friends", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Friends");
                var friends = host.AddComponent<FriendsPresenter>();
                friends.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(FriendsPresenter.CanvasName);
            }),
            new UiScreenEntry("VipSubscription", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_VipSubscription");
                var vip = host.AddComponent<VipSubscriptionPresenter>();
                vip.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(VipSubscriptionPresenter.CanvasName);
            }),
            new UiScreenEntry("PermitWeekKey", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_PermitWeekKey");
                var permit = host.AddComponent<PermitWeekKeyPresenter>();
                permit.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(PermitWeekKeyPresenter.CanvasName);
            }),
            new UiScreenEntry("SpellLoadoutPicker", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_SpellLoadoutPicker");
                var spell = host.AddComponent<SpellLoadoutPickerPresenter>();
                spell.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(SpellLoadoutPickerPresenter.CanvasName);
            }),
            new UiScreenEntry("ChatSocial", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_ChatSocial");
                var chat = host.AddComponent<ChatSocialPresenter>();
                chat.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(ChatSocialPresenter.CanvasName);
            }),
            new UiScreenEntry("Bazaar", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_Bazaar");
                var bazaar = host.AddComponent<BazaarPresenter>();
                bazaar.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(BazaarPresenter.CanvasName);
            }),
            new UiScreenEntry("MemoryExpedition", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_MemoryExpedition");
                var mem = host.AddComponent<MemoryExpeditionPresenter>();
                mem.Initialize(onBack: probe.On("onBack"));
                return GameObject.Find(MemoryExpeditionPresenter.CanvasName);
            }),
            new UiScreenEntry("SoloCircuit", UiSurfaceKind.Secondary, (newHost, probe) =>
            {
                GameObject host = newHost("CS_SoloCircuit");
                var circuit = host.AddComponent<SoloCircuitPresenter>();
                circuit.Initialize(SaveSystem.CurrentProfile, DateTime.UtcNow, onBack: probe.On("onBack"));
                return GameObject.Find(SoloCircuitPresenter.CanvasName);
            }),
        };
    }
}
