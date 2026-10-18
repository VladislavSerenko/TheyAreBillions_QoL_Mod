namespace TABQoL
{
    // Symbol names only. No game implementation or assets are distributed.
    public static class Compatibility
    {
        public const string OriginalSha256 = "f502e38dd6840a5d52331c1c8d68ab94c79f620e2dd5453e9bf5168d909433d3";
        public const string GameVersion = "1.1.4.10 (GOG x64)";
        public const string SteamSha256 = "695cef4ad913f9ffd1f9f0508d26862546b130922db53dce716051fe9b0a9d2d";
        public const string SteamVersion = "Steam x64 (EXE 1.0.14.29)";
        internal static bool RuntimeSteam;

        public static bool Supported(string hash) { return hash == OriginalSha256 || hash == SteamSha256; }
        public static string VersionFor(string hash) { return hash == SteamSha256 ? SteamVersion : GameVersion; }
        public static string Symbol(string name, bool steam)
        {
            string mapped;
            return steam && SteamSymbols.Names.TryGetValue(name, out mapped) ? mapped : name;
        }
        internal static string RuntimeSymbol(string name) { return Symbol(name, RuntimeSteam); }
        public const string Game = "#=zDWKQa9iNgGJZwrF26g==";
        public const string MissionSystem = "#=zSz0PIPoe$1bzpCj9VL55$uw=";
        public const string WorldSystem = "#=zrkMXvYMLBXvSpF4_wXnuWWSJBi98";
        public const string Current = "#=zKKcS4uw=";
        public const string Save = "#=zjpIhLbTrXdTk";
        public const string Load = "#=zdXhkDGBAxYZXiuzHeg==";
        public const string SavesDirectory = "#=zXNSl029mPMOY6Jn_Ng==";
        public const string CaptureLevel = "#=z1bNOp4aWqr$i";
        public const string WriteSave = "#=z_F7azNTGCKsjCnAtcA==";
        public const string ReadSave = "#=zvT_u6MS_DQf7qXj2nQ==";
        public const string SetState = "#=zPfDvqoE=";
        public const string CompleteMission = "#=zl_ZGp3jyTOIKGT0_gw==";
        public const string RefundResearch = "#=zLMShEFC9jpaO4VPMB0gqR_U=";
        public const string RebuildEntities = "#=zizPJBb5xp65J";
        public const string RefreshWorld = "#=z05IuJj__BbzYhHsjMg==";
        public const string RefreshResearch = "#=zPvzf4cLsz8Wu48qHBQ==";
        public const string ShowWorld = "#=zazFoTtSyIlJm";
        public const string StartMissionClosure = "<>c__DisplayClass64_0";
        public const string StartMission = "#=zecoAwxI00vS7vQao43Sgb4AXv8bq2eHvtg==";
        public const string Options = "#=zqWh9dpSyGP7V";
        public const string AddTab = "#=zAiew5ms=";
        public const string ShowWindow = "#=z9MozRH4=";
        public const string CloseWindow = "#=zJZkqJ1c=";
        public const string CloseButton = "#=zUKXJUrJtBsnw";
        public const string Fonts = "#=zqZKzsA47yWa6+#=zz8951q8=";
        public const string BodyFont = "#=z7HoVskmyZLzk";
        public const string Notify = "#=z8OaGcNk=";
        public const string LifeMeters = "#=zmObRylW7HuR6ZJIz_dWqfbw=";
    }
}
