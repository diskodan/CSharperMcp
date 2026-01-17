namespace ProjectWithErrors;

public class ClassWithErrors
{
    public void MethodWithUndeclaredVariable()
    {
        // CS0103: The name 'undeclaredVariable' does not exist in the current context
        Console.WriteLine(undeclaredVariable);
    }

    public void MethodWithTypeMismatch()
    {
        // CS0029: Cannot implicitly convert type 'string' to 'int'
        int number = "not a number";
    }

    public void MethodWithMissingMethod()
    {
        // CS1061: 'string' does not contain a definition for 'NonExistentMethod'
        string text = "hello";
        text.NonExistentMethod();
    }
}
