namespace RoseClassic.PacketIO
{
    /// <summary>
    /// Fixed struct sizes from rose-next-classic net_prototype.h / cuserdata.h (pack=1).
    /// </summary>
    public static class RoseStructSizes
    {
        public const int TagCharInfo = 10; // race + level + job + remainSec + platinum
        public const int TagPartItem = 4;
        public const int BodyPartCount = 10;

        // gsv_SELECT_CHAR fixed body before appended szCharName[].
        public const int GsvSelectCharFixedBody =
            1 + 2 + 8 + 2 + // race, zone, pos, revive zone
            (TagPartItem * BodyPartCount) * 2 + // parts + costume
            8 + 12 + // tagBasicINFO + tagBasicAbility
            383 + // tagGrowAbility
            240 + // tagSkillAbility (120 shorts)
            128 + // CHotICONS (64 x WORD)
            4; // m_dwUniqueTAG
    }
}
