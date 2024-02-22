public static class TBillHelper
{
    public static DateTime GetEffectiveDate(DateTime date)
    {
        // Get today's date and the current day of the week
        var Today = date;
        DayOfWeek currentDay = Today.DayOfWeek;

        // Calculate the start of the current week (Sunday)
        var startOfCurrentWeek = Today.AddDays(-(int)Today.DayOfWeek + (int)DayOfWeek.Sunday);

        // Calculate the start of the last week
        var startOfLastWeek = startOfCurrentWeek.AddDays(-7);

        // Determine the Thursday of the current week
        var thursdayOfCurrentWeek = startOfCurrentWeek.AddDays((int)DayOfWeek.Thursday - (int)DayOfWeek.Sunday);

        DateTime effectiveStartDate;
        if (Today < thursdayOfCurrentWeek)
        {
            // If today is before Thursday, use the T-Bill from the previous week
            effectiveStartDate = startOfLastWeek;
        }
        else
        {
            // If today is Thursday or later, use this week's T-Bill
            effectiveStartDate = startOfCurrentWeek;
        }

        return effectiveStartDate;
    }

    public static (DateTime cycleStart, DateTime cycleEnd) GetCurrentTBillCycle(DateTime date)
    {
        DateTime cycleStart;
        DateTime cycleEnd;

        // Check if the given date is Thursday
        if (date.DayOfWeek == DayOfWeek.Thursday)
        {
            // If it's Thursday, the cycle starts today
            cycleStart = date;
        }
        else
        {
            // If it's not Thursday, find the most recent Thursday. Thats when the cycle started for this date
            int daysSinceLastThursday = (int)date.DayOfWeek - (int)DayOfWeek.Thursday;

            if (daysSinceLastThursday < 0)
            {
                // If the given day is before Thursday in the week, adjust the value
                daysSinceLastThursday += 7;
            }

            cycleStart = date.AddDays(-daysSinceLastThursday);
        }

        // The cycle ends on the Wednesday just after the cycle start, which is 6 days after the cycle start
        cycleEnd = cycleStart.AddDays(6);

        return (cycleStart, cycleEnd);
    }




    public static (DateTime cycleStart, DateTime cycleEnd) GetPreviousTBillCycle(DateTime date)
    {

        var (startOfCycle, endOfCycle) = GetCurrentTBillCycle(date);
        var previousCycleStart = startOfCycle.AddDays(-7);
        var previousCycleEnd = endOfCycle.AddDays(-7);

        return (previousCycleStart, previousCycleEnd);
    }




}