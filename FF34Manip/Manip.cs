namespace FF34Manip
{
    public struct Manip
    {
        // Property rather than a field so the button label can bind to it
        public string Name { get; set; }
        public string TimeZone;
        public short Day;
        public short Month;
        public short Year;
        public short Hour;
        public short Minute;
        public short Second;

        public Manip(string name, string timeZone, short dd, short MM, short yyyy, short HH, short mm, short ss)
        {
            Name = name;
            TimeZone = timeZone;
            Day = dd;
            Month = MM;
            Year = yyyy;
            Hour = HH;
            Minute = mm;
            Second = ss;
        }
    }
}
