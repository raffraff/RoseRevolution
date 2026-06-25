using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Simple IMGUI flow for login, server select, and character select.
/// </summary>
public class RoseLoginUI : MonoBehaviour
{
    [Header("Defaults")]
    public string defaultUsername = "test";
    public string defaultPassword = "test";

    [Header("Window")]
    public Vector2 initialWindowPosition = new Vector2(20, 20);
    public Vector2 windowSize = new Vector2(360, 420);

    string username = "test";
    string password = "test";
    string status = "Not connected";
    string createName = "Hero";
    int selectedServerIndex;
    int selectedCharacterIndex = -1;
    short createHair = 1;
    short createFace = 1;
    byte createGender;

    private bool isOpenWindow = true;

    Rect windowRect;

    enum UiScreen { Login, Servers, Characters, CreateCharacter, InGame }
    UiScreen screen = UiScreen.Login;

    void Start()
    {
        username = defaultUsername;
        password = defaultPassword;
        windowRect = new Rect(initialWindowPosition.x, initialWindowPosition.y, windowSize.x, windowSize.y);

        if (RoseClassic.RoseNetworkManager.Instance != null)
        {
            var net = RoseClassic.RoseNetworkManager.Instance;
            net.StatusChanged += s => status = s;
            net.LoginFailed += msg => status = msg;
            net.ServerListUpdated += () => screen = UiScreen.Servers;
            net.CharacterListUpdated += () => screen = UiScreen.Characters;
            net.EnteredWorld += () => screen = UiScreen.InGame;
            net.CharacterReadyToEnter += () => status = "Entering map...";
        }
    }

    void OnGUI()
    {
        if (isOpenWindow)
        {
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawLoginWindow, "Rose Revolution Login");
        }
    }

    void DrawLoginWindow(int windowId)
    {
        GUILayout.Label(status);

        switch (screen)
        {
            case UiScreen.Login:
                DrawLoginScreen();
                break;
            case UiScreen.Servers:
                DrawServerScreen();
                break;
            case UiScreen.Characters:
                DrawCharacterScreen();
                break;
            case UiScreen.CreateCharacter:
                DrawCreateCharacterScreen();
                break;
            case UiScreen.InGame:
                isOpenWindow = false;
                //GUILayout.Label("In world. Use chat and click to move.");
                break;
        }

        GUI.DragWindow(new Rect(0, 0, windowRect.width, 24));
    }

    void DrawLoginScreen()
    {
        username = GUILayout.TextField(username);
        password = GUILayout.PasswordField(password, '*');

        if (GUILayout.Button("Connect && Login") && RoseClassic.RoseNetworkManager.Instance != null)
            _ = LoginAsync();
    }

    async Task LoginAsync()
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        status = "Connecting...";

        try
        {
            await net.ConnectLoginAsync(username, password);
            await Task.Delay(100);
            net.SendLogin(username, password);
        }
        catch (System.Exception ex)
        {
            status = ex.Message;
        }
    }

    void DrawServerScreen()
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        if (net == null)
            return;

        for (int i = 0; i < net.Servers.Count; i++)
        {
            if (GUILayout.Toggle(selectedServerIndex == i, net.Servers[i].Name))
                selectedServerIndex = i;
        }

        if (GUILayout.Button("Select Server") && net.Servers.Count > 0)
            net.SelectServer(net.Servers[selectedServerIndex].Id, net.defaultChannel);
    }

    void DrawCharacterScreen()
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        if (net == null)
            return;

        for (int i = 0; i < net.Characters.Count; i++)
        {
            var character = net.Characters[i];
            if (GUILayout.Toggle(selectedCharacterIndex == i, $"[{character.Slot}] {character.Name} Lv{character.Level}"))
                selectedCharacterIndex = i;
        }

        if (GUILayout.Button("Enter World") && selectedCharacterIndex >= 0 && selectedCharacterIndex < net.Characters.Count)
        {
            var character = net.Characters[selectedCharacterIndex];
            net.SelectCharacter(character.Slot, character.Name);
        }

        if (GUILayout.Button("Create Character"))
            screen = UiScreen.CreateCharacter;
    }

    void DrawCreateCharacterScreen()
    {
        createName = GUILayout.TextField(createName);
        short.TryParse(GUILayout.TextField(createHair.ToString()), out createHair);
        short.TryParse(GUILayout.TextField(createFace.ToString()), out createFace);
        createGender = (byte)(GUILayout.Toggle(createGender == 1, "Female") ? 1 : 0);

        if (GUILayout.Button("Create") && RoseClassic.RoseNetworkManager.Instance != null)
        {
            RoseClassic.RoseNetworkManager.Instance.CreateCharacter(createName, createHair, createFace, 0, createGender);
            screen = UiScreen.Characters;
        }

        if (GUILayout.Button("Back"))
            screen = UiScreen.Characters;
    }
}
