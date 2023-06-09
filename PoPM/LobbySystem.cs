using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PoPM
{
    /// <summary>
    /// Setting some flags for lobby logic 
    /// </summary>
    [HarmonyPatch(typeof(MainMenu), "Initialize")]
    public class MainMenuInitializePatch
    {
        static void Prefix()
        {
            LobbySystem.Instance.mainMenu = GameObject.Find("Menu/Canvas/MainMenu/VerticalGroup/");
            SteamMatchmaking.SetLobbyData(LobbySystem.Instance.actualLobbyID, "started", "false");

            if (!LobbySystem.Instance.isPauseMenu)
            {
                NameTag.CreateCanvas();
                LobbySystem.Instance.isInGame = false;
                LobbySystem.Instance.isGameLoaded = true;
            }
        }
    }

    [HarmonyPatch(typeof(MainMenu), "ChangeToPauseMenu")]
    public class MainMenuChangeToPauseMenuPatch
    {
        static void Prefix()
        {
            LobbySystem.Instance.isPauseMenu = true;
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Play")]
    public class OnStartPlayPatch
    {
        static void Prefix()
        {
            LobbySystem.Instance.isPauseMenu = false;
            LobbySystem.Instance.isInGame = true;
            LobbySystem.Instance.GUIStack.Clear();
        }

        static void Postfix()
        {
            if (!LobbySystem.Instance.isInLobby || IngameNetManager.Instance.isClient)
                return;

            if (LobbySystem.Instance.isLobbyOwner)
            {
                IngameNetManager.Instance.OpenRelay();
                IngameNetManager.Instance.StartAsServer();
                SteamMatchmaking.SetLobbyData(LobbySystem.Instance.actualLobbyID, "started", "yes");
            }
            else
                IngameNetManager.Instance.StartAsClient(LobbySystem.Instance.ownerID);

            NetVillager.GetOwnTransform();
            LobbySystem.Instance.isInGame = true;
        }
    }


    public class LobbySystem : MonoBehaviour
    {
        public static LobbySystem Instance;

        public string joinLobbyID = string.Empty;
        public CSteamID actualLobbyID = CSteamID.Nil;
        public CSteamID ownerID = CSteamID.Nil;
        public string ownerName;

        public int maxLobbyMembers = 250;

        public bool isPauseMenu;
        public bool isInGame;
        public bool isGameLoaded;
        public bool isInLobby;
        public bool isLobbyOwner;

        public GameObject mainMenu;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;

        // idfk why making dummy vars assigns for the callback but it somehow makes the callbacks be called
        private Callback<LobbyEnter_t> _lobbyEnter;

        private TimedAction delayPlay = new(1f);

        public Stack<string> GUIStack = new();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

            if (!isInLobby)
            {
                if (GUIStack.Count == 0)
                    GUIStack.Push("Main");
                else
                    GUIStack.Clear();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M) && !isInLobby && !isInGame)
            {
                if (GUIStack.Count == 0)
                    GUIStack.Push("Main");
                else
                    GUIStack.Clear();
            }

            if (SteamMatchmaking.GetLobbyData(actualLobbyID, "started") == "yes" && !isLobbyOwner && !isInGame)
            {
                delayPlay.Start();
                isInGame = true;
            }

            if (delayPlay.TrueDone())
            {
                FindObjectOfType<MainMenu>().Play(true);
                delayPlay.TurnOff();
            }
        }

        private void OnGUI()
        {
            var lobbyStyle = new GUIStyle(GUI.skin.box);

            if (!isInGame && isGameLoaded && GUIStack.Count != 0)
            {
                GUILayout.BeginArea(new Rect((Screen.width - 220), (Screen.height - 550), 150f, 500), string.Empty);
                GUILayout.BeginVertical(lobbyStyle);
                switch (GUIStack.Peek())
                {
                    case "Main":
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("<color=white>PoPM</color>");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(15f);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("JOIN"))
                            GUIStack.Push("Join");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5f);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("HOST"))
                            GUIStack.Push("Host");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        break;
                    }
                    case "Host":
                    {
                        if (!isInLobby)
                        {
                            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, maxLobbyMembers);

                            isInLobby = true;
                            isLobbyOwner = true;
                        }

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"HOST");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5f);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Lobby ID: {actualLobbyID.GetAccountID()}");

                        if (GUILayout.Button("Copy ID"))
                        {
                            GUIUtility.systemCopyBuffer = actualLobbyID.GetAccountID().ToString();
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(15f);

                        if (GUILayout.Button("<color=#ed0e0e>EXIT</color>"))
                        {
                            GUIStack.Pop();
                        }

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"PLAYERS");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5f);

                        foreach (CSteamID member in GetLobbyMembers().ToArray())
                        {
                            var memberName = SteamFriends.GetFriendPersonaName(new CSteamID(member.m_SteamID));
                            GUILayout.Label(memberName);
                        }

                        break;
                    }
                    case "Join":
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"JOIN");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(15f);

                        joinLobbyID = GUILayout.TextField(joinLobbyID);

                        GUILayout.Space(5f);

                        if (GUILayout.Button("JOIN"))
                        {
                            if (uint.TryParse(joinLobbyID, out uint idLong))
                            {
                                CSteamID lobbyId = new CSteamID(new AccountID_t(idLong),
                                    (uint) EChatSteamIDInstanceFlags.k_EChatInstanceFlagLobby |
                                    (uint) EChatSteamIDInstanceFlags.k_EChatInstanceFlagMMSLobby,
                                    EUniverse.k_EUniversePublic, EAccountType.k_EAccountTypeChat);
                                SteamMatchmaking.JoinLobby(lobbyId);
                            }

                            isInLobby = true;
                            isLobbyOwner = false;
                            GUIStack.Push("Guest");
                        }

                        if (GUILayout.Button("<color=#888888>BACK</color>"))
                        {
                            GUIStack.Pop();
                        }

                        break;
                    }
                    case "Guest":
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"{ownerName} Lobby");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        if (GUILayout.Button("<color=#ed0e0e>EXIT</color>"))
                        {
                            ExitLobby();
                            GUIStack.Pop();
                        }

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"PLAYERS");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5f);

                        foreach (CSteamID member in GetLobbyMembers().ToArray())
                        {
                            var memberName = SteamFriends.GetFriendPersonaName(new CSteamID(member.m_SteamID));
                            GUILayout.Label(memberName);
                        }

                        break;
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        public List<CSteamID> GetLobbyMembers()
        {
            int len = SteamMatchmaking.GetNumLobbyMembers(actualLobbyID);
            var ret = new List<CSteamID>(len);

            for (int i = 0; i < len; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(actualLobbyID, i);
                ret.Add(member);
            }

            return ret;
        }

        public void ExitLobby()
        {
            if (!isInLobby)
                return;

            if (GUIStack.Count != 0)
            {
                if (GUIStack.Peek() == "Guest" || GUIStack.Peek() == "Host")
                {
                    GUIStack.Pop();
                }
            }

            isInLobby = false;
            isLobbyOwner = false;

            Plugin.Logger.LogInfo($"Leaving lobby! {actualLobbyID.GetAccountID()}");
            SteamMatchmaking.LeaveLobby(actualLobbyID);
            IngameNetManager.Instance.ResetState();

            mainMenu.transform.GetChild(0).GetComponent<Image>().enabled = false;
            mainMenu.transform.GetChild(0).GetComponent<Button>().enabled = false;
            mainMenu.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1f);

            ownerName = string.Empty;
            actualLobbyID = CSteamID.Nil;
        }

        public void OnLobbyCreated(LobbyCreated_t pCallback)
        {
            actualLobbyID = new CSteamID(pCallback.m_ulSteamIDLobby);
            Plugin.Logger.LogInfo($"Created lobby! {actualLobbyID.GetAccountID()}");
        }

        public void OnLobbyEnter(LobbyEnter_t pCallback)
        {
            actualLobbyID = new CSteamID(pCallback.m_ulSteamIDLobby);
            Plugin.Logger.LogInfo($"Joined lobby! {actualLobbyID.GetAccountID()}");

            ownerID = SteamMatchmaking.GetLobbyOwner(actualLobbyID);
            ownerName = SteamFriends.GetFriendPersonaName(ownerID);

            Plugin.Logger.LogInfo($"Host ID: {ownerID}");
            Plugin.Logger.LogInfo($"Host Name: {ownerName}");

            if (!isLobbyOwner)
            {
                mainMenu.transform.GetChild(0).GetComponent<Image>().enabled = false;
                mainMenu.transform.GetChild(0).GetComponent<Button>().enabled = false;
                mainMenu.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().color =
                    new Color(1, 1, 1, 0.2f);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            // Anything other than a join...
            if ((pCallback.m_rgfChatMemberStateChange &
                 (uint) EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0)
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                // ...means the owner left.
                if (ownerID == id)
                {
                    ExitLobby();
                }
            }
        }

        public void RestartMenu()
        {
            Plugin.Logger.LogInfo("Restarting");
            var menu = FindObjectOfType<MainMenu>();
            menu.isActive = true;
            typeof(MainMenu).GetField("restarting", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(menu, false);
            typeof(MainMenu).GetMethod("Restart", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, new object[] { });
        }
    }
}