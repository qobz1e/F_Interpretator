using F_Interpretator;


internal class Program
{
    private static void Main(string[] args)
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var projectRoot = Directory.GetParent(baseDirectory)?.Parent?.Parent?.FullName;

        // var sourcePath = Path.Combine(currentDirectory, "examples/test16.fl");

        var examplesFolder = Path.Combine(projectRoot, "examples");

        var files = Directory.GetFiles(examplesFolder, "*.fl")
                   .OrderBy(f => {
                       var name = Path.GetFileNameWithoutExtension(f);
                       if (name.StartsWith("test") && int.TryParse(name[4..], out int num))
                           return num;
                       return int.MaxValue; // остальные файлы в конец
                   })
                   .ToArray();

        foreach (var filePath in files)
        {
            var source = File.ReadAllText(filePath);
            Console.WriteLine($"=== Processing: {Path.GetFileName(filePath)} ===");
            Console.WriteLine(source);
            Console.WriteLine("--- TOKENS ---");

            var lexer = new Lexer(source);
            Token token;

            do
            {
                token = lexer.Lex();
                Console.WriteLine(token.ToString());
            } while (token.tokenType != TokenType.EOF);

            Console.WriteLine("=====================\n");
        }

        Console.ReadKey();
    }
}