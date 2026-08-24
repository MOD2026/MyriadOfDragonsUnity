using MyriadOfDragons.Save;
using UnityEngine;
using Unity.Services.Authentication;

namespace MyriadOfDragons.UI
{
    /// <summary>Account/session actions from Settings — sign-out when Unity Authentication is available.</summary>
    public static class AccountSessionService
    {
        public static bool TryLogout(PlayerProfile profile, out string statusMessage)
        {
            statusMessage = null;
            if (profile != null)
                SaveSystem.Save(profile);

            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut(true);
                    statusMessage = "Signed out of your account.";
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Account logout: Unity Authentication sign-out failed ({ex.Message}).");
            }

            statusMessage = "Local session cleared.";
            return true;
        }
    }
}
