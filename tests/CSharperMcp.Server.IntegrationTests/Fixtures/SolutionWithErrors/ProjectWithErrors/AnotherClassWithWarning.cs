namespace ProjectWithErrors;

public class AnotherClassWithWarning
{
    // CS0414: The field is assigned but its value is never used (warning)
    private int unusedField = 42;

    public void ValidMethod()
    {
        // This method is valid
        Console.WriteLine("This is fine");
    }
}
