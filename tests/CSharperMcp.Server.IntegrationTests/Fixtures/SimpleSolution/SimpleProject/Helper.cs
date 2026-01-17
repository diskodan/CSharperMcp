namespace SimpleProject;

public static class Helper
{
    public static string FormatResult(string operation, int result)
    {
        return $"{operation} = {result}";
    }

    public static bool IsEven(int number) => number % 2 == 0;
}
