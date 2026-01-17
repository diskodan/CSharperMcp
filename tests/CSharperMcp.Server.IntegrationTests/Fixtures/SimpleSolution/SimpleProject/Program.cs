namespace SimpleProject;

public class Program
{
    public static void Main()
    {
        var calc = new Calculator();

        // Multiple references to Calculator.Add method
        var result1 = calc.Add(1, 2);
        var result2 = calc.Add(3, 4);
        var result3 = calc.Add(5, 6);

        Console.WriteLine(result1);
        Console.WriteLine(result2);
        Console.WriteLine(result3);
    }
}
