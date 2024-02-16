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

    public static (DateTime cycleStart, DateTime cycleEnd) GetTBillCycle(DateTime date)
    {
        // Find the number for Thursday (which is 4)
        int numberForThursday = (int)DayOfWeek.Thursday;

        // Find the number for the current day of the week
        int numberForCurrentDay = (int)date.DayOfWeek;

        // Calculate the initial difference in days between the current day and Thursday
        int initialDifference = numberForThursday - numberForCurrentDay;

        // Add 7 days to ensure we're looking forward to the next Thursday, not backward
        int differencePlusWeek = initialDifference + 7;

        // Use modulo to wrap around the week if necessary (to stay within 0-6 range)
        int daysUntilThursday = differencePlusWeek % 7;

        DateTime cycleEnd = date.AddDays(daysUntilThursday);

        // If the given date is Thursday, it is the end of the cycle, so no need to add days
        if (date.DayOfWeek == DayOfWeek.Thursday)
        {
            cycleEnd = date;
        }

        // The cycle starts on the Friday of the previous week
        DateTime cycleStart = cycleEnd.AddDays(-6);

        return (cycleStart, cycleEnd);
    }

    public static (DateTime cycleStart, DateTime cycleEnd) GetPreviousTBillCycle(DateTime date)
    {
        // Find the number for Thursday (which is 4)
        int numberForThursday = (int)DayOfWeek.Thursday;

        // Find the number for the current day of the week
        int numberForCurrentDay = (int)date.DayOfWeek;

        // Calculate the difference in days between the current day and the last Thursday
        // This might be negative if the current day is after Thursday, so we add 7 and then take modulo 7 to correct it
        int differenceToLastThursday = (numberForCurrentDay - numberForThursday + 7) % 7;

        // If today is Thursday, differenceToLastThursday will be 0, meaning we don't need to subtract any days to get to Thursday
        // Otherwise, we subtract the difference to get to the last Thursday
        DateTime currentCycleEnd = date.AddDays(-differenceToLastThursday);

        // The cycle starts on the Friday of the same week as the last Thursday
        DateTime currentCycleStart = currentCycleEnd.AddDays(-6);

        // To get the previous cycle, subtract 7 days from the start and end of the current cycle
        DateTime previousCycleEnd = currentCycleEnd.AddDays(-7);
        DateTime previousCycleStart = currentCycleStart.AddDays(-7);

        return (previousCycleStart, previousCycleEnd);
    }




}