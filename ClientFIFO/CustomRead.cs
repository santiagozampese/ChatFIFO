using System.Text;
public static class CustomRead
{
    public static StringBuilder stringBuilder = new();
    public static List<ConsoleKey> specialKeys = new() {ConsoleKey.Enter, ConsoleKey.Backspace};
    public static string ReadLine(string text = "")
    {
        ConsoleKeyInfo keyInfo = new();
        while (keyInfo.Key != ConsoleKey.Enter)
        {
            keyInfo = Console.ReadKey();

            if (!specialKeys.Contains(keyInfo.Key))
            {
                stringBuilder.Append(keyInfo.KeyChar);
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (stringBuilder.Length >= 1)
                {                
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                }
            }

            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            Console.Write($"\r{text}{stringBuilder.ToString()}");
        }

        var msg = stringBuilder.ToString();
        stringBuilder.Clear();
        return msg;
    }
}