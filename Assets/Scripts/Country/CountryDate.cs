using System;

public readonly struct CountryDate : IComparable<CountryDate>
{
    public readonly int MonthIndex, Month, Day;
    public CountryDate PlanningStart => Day == 1 ? this : new CountryDate(checked(MonthIndex + 1), Month % 12 + 1, 1);
    public CountryDate(int monthIndex, int month, int day)
    {
        if (monthIndex < 0 || month < 1 || month > 12 || day < 1 || day > DateManager.GetDaysInMonth(month))
            throw new ArgumentOutOfRangeException(nameof(day));
        MonthIndex = monthIndex; Month = month; Day = day;
    }

    public CountryDate AddMonths(int months)
    {
        if (months < 0) throw new ArgumentOutOfRangeException(nameof(months));
        int month = (Month - 1 + months) % 12 + 1;
        return new CountryDate(checked(MonthIndex + months), month, Math.Min(Day, DateManager.GetDaysInMonth(month)));
    }

    public CountryDate AddDays(int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
        int index = MonthIndex, month = Month, day = Day;
        while (days > DateManager.GetDaysInMonth(month) - day)
        {
            days -= DateManager.GetDaysInMonth(month) - day + 1;
            index = checked(index + 1); month = month % 12 + 1; day = 1;
        }
        return new CountryDate(index, month, day + days);
    }

    public int CompareTo(CountryDate other) => MonthIndex == other.MonthIndex ? Day.CompareTo(other.Day) : MonthIndex.CompareTo(other.MonthIndex);
    public int DaysUntil(CountryDate other)
    {
        int days = other.Day - Day;
        for (int i = 0; i < other.MonthIndex - MonthIndex; i++) days += DateManager.GetDaysInMonth((Month - 1 + i) % 12 + 1);
        return days;
    }
    public override string ToString() => $"{Month}月{Day}日";
    public static CountryDate From(DateManager date) => new(date.ElapsedMonths, date.CurrentMonth, date.CurrentDay);
}
