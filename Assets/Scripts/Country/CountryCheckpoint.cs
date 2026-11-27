using System;
using System.IO;

[Serializable]
internal sealed class CountryCheckpoint
{
    internal const int LegacyCPVersion = 1;
    internal const int CurrentVersion = 2;
    public int Version;
    public long Revision;
    public Date Day;
    public double DayProgress;
    public int Population, Treasury, Wood, Ore;
    public int TaxRemainder, WoodRemainder, OreRemainder;
    public CountryIncomeReceipt Income;
    public RosterState Roster;
    public WorldState World;
    public GovernmentState Government;

    internal static void Require(bool valid, string field)
    {
        if (!valid) throw new InvalidDataException("Invalid country checkpoint: " + field);
    }

    [Serializable]
    internal struct Date
    {
        public int MonthIndex, Month, Day;
        public Date(CountryDate date) => (MonthIndex, Month, Day) = (date.MonthIndex, date.Month, date.Day);
        public CountryDate ToDate()
        {
            Require(MonthIndex >= 0 && Month is >= 1 and <= 12 && Day >= 1 &&
                Day <= DateManager.GetDaysInMonth(Month), "date");
            return new CountryDate(MonthIndex, Month, Day);
        }
        public CountryDate? OptionalDate() => Month == 0 && MonthIndex == 0 && Day == 0 ? null : ToDate();
    }

    [Serializable]
    internal struct RosterState
    {
        public Soldier[] Soldiers;
        public Weapon[] Weapons;
        public int LastForgeMonth;
        public string[] BattleNotices;
    }

    [Serializable]
    internal struct Soldier
    {
        public string Id;
        public int CP;
        public CombatAiPersonalityKind Personality;
        public Date RevivalDue;
        public SchoolLevel[] Completed;
        public SkillId[] Learned;
        public string TrainingSchool;
        public int TrainingLevel;
        public Date TrainingStart;
        public SkillId[] TrainingSkills;
    }

    [Serializable]
    internal struct SchoolLevel
    {
        public string School;
        public int Level;
    }

    [Serializable]
    internal struct Weapon
    {
        public string Template, Owner;
        public int Durability;
    }

    [Serializable]
    internal struct WorldState
    {
        public string[] Conquered;
        public Trip[] Active, Returned;
        public DefenseState Defense;
    }

    [Serializable]
    internal struct Trip
    {
        public string Target;
        public Date Departure;
        public int Days;
        public string[] Members;
        public int[] Weapons;
        public Result Outcome;
    }

    internal enum Result { Pending, Defeat, Victory }

    [Serializable]
    internal struct DefenseState
    {
        public bool Enabled;
        public string[] Members;
        public Date Today, NextRaid;
        public string RaidRegion;
        public Date RaidDate;
    }

    [Serializable]
    internal struct GovernmentState
    {
        public Facility[] Facilities;
        public FundingOrder[] ScheduledFunding;
        public Construction[] Construction;
        public bool UseTreasury;
        public int ReportMonth, FundedMonth;
        public string[] Report;
    }

    [Serializable]
    internal struct Facility
    {
        public string Name;
        public int Level, Funding, DraftFunding;
    }

    [Serializable]
    internal struct FundingOrder
    {
        public int Month;
        public Funding[] Amounts;
    }

    [Serializable]
    internal struct Funding
    {
        public string Facility;
        public int Amount;
    }

    [Serializable]
    internal struct Construction
    {
        public string Facility;
        public CountryGovernmentCost Cost;
        public Date Start, Due;
        public bool Started;
    }
}
