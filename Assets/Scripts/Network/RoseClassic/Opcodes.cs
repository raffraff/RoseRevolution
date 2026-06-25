namespace RoseClassic
{
    public static class Opcodes
    {
        public const ushort SocketNetworkStatus = 0x07ff;

        public const ushort CliAlive = 0x0700;
        public const ushort SrvError = 0x0700;
        public const ushort CliAcceptReq = 0x0703;
        public const ushort CliChannelListReq = 0x0704;
        public const ushort LsvChannelListReply = 0x0704;
        public const ushort CliLoginReq = 0x0708;
        public const ushort LsvLoginReply = 0x0708;
        public const ushort CliSelectServer = 0x070a;
        public const ushort LsvSelectServer = 0x070a;
        public const ushort CliJoinServerReq = 0x070b;
        public const ushort SrvJoinServerReply = 0x070c;
        public const ushort WsvMoveServer = 0x0711;
        public const ushort WsvMemo = 0x07e5;
        public const ushort CliCharList = 0x0712;
        public const ushort WsvCharList = 0x0712;
        public const ushort WsvCreateChar = 0x0713;
        public const ushort CliDeleteChar = 0x0714;
        public const ushort WsvDeleteChar = 0x0714;
        public const ushort CliSelectChar = 0x0715;
        public const ushort GsvSelectChar = 0x0715;
        public const ushort GsvInventoryData = 0x0716;
        public const ushort GsvQuestData = 0x071b;
        public const ushort GsvQuestOnly = 0x0723;
        public const ushort GsvWishList = 0x0724;
        public const ushort CliJoinZone = 0x0753;
        public const ushort GsvJoinZone = 0x0753;
        public const ushort GsvInitData = 0x0754;
        public const ushort CliChat = 0x0783;
        public const ushort GsvChat = 0x0783;
        public const ushort GsvNpcChar = 0x0791;
        public const ushort GsvMobChar = 0x0792;
        public const ushort GsvAvtChar = 0x0793;
        public const ushort GsvSubObject = 0x0794;
        public const ushort CliStop = 0x0796;
        public const ushort GsvStop = 0x0796;
        public const ushort GsvMove = 0x0797;
        public const ushort CliMouseCmd = 0x079a;
        public const ushort GsvMouseCmd = 0x079a;
    }

    public static class NetworkStatus
    {
        public const byte Connect = 0x01;
        public const byte Accepted = 0x02;
        public const byte Disconnect = 0x03;
        public const byte ServerDead = 0x04;
    }

    public static class LoginResult
    {
        public const byte Ok = 0x00;
        public const byte Failed = 0x01;
        public const byte NotFoundAccount = 0x02;
        public const byte InvalidPassword = 0x03;
        public const byte AlreadyLoggedIn = 0x04;
    }

    public static class SelectServerResult
    {
        public const byte Ok = 0x00;
        public const byte Failed = 0x01;
        public const byte Full = 0x02;
    }

    public static class JoinServerResult
    {
        public const byte Ok = 0x00;
        public const byte Failed = 0x01;
        public const byte InvalidPassword = 0x03;
    }

    public static class CreateCharResult
    {
        public const byte Ok = 0x00;
    }

    public static class RosePorts
    {
        public const int Login = 29000;
        public const int World = 29100;
        public const int Game = 29200;
    }

    public static class BodyPart
    {
        public const int Max = 10;
        public const int Face = 0;
        public const int Hair = 1;
        public const int Helmet = 2;
        public const int Armor = 3;
        public const int Gauntlet = 4;
        public const int Boots = 5;
        public const int Goggle = 6;
        public const int Knapsack = 7;
        public const int WeaponR = 8;
        public const int WeaponL = 9;
    }
}
