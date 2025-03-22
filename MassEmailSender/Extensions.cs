using System.Text.RegularExpressions;

namespace MassEmailSender;

public static partial class Extensions
{
    private static readonly Random Random = new();
    
    public static List<T>? Shuffle<T>(this List<T>? list)
    {
        if (list == null || list.Count == 0)
        {
            return null;
        }
        List<T> shuffledList = [..list];
        var n = shuffledList.Count;
        while (n > 1)
        {
            n--;
            var randomValue = Random.Next(n + 1);
            (shuffledList[randomValue], shuffledList[n]) = (shuffledList[n], shuffledList[randomValue]);
        }
        return shuffledList;
    }
    
    public static bool ValidateEmail(this string str)
    {
        return MyRegex().IsMatch(str);
    }
    
    [GeneratedRegex("^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\\.[a-zA-Z0-9-]+)*$")]
    private static partial Regex MyRegex();
}