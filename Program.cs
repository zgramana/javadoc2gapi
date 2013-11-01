using System;
using System.ComponentModel;
using Args;
using Args.Help;
using System.ComponentModel.DataAnnotations;
using HtmlAgilityPack;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace JavaDoc2Metadata
{
    [Description("JavaDoc2Metadata - parses Javadoc HTML and exports custom GAPI attribute nodes with custom field names.")]
    [ArgsModel(SwitchDelimiter = "-")]
    class CommandArgs
    {
        [Required, Description("Path to the javadoc HTML containing field names.")]
        public string JavadocPath { get; set; }
        [Required, Description("The java package name.")]
        // TODO: Parse this out of the javadoc.
        public string PackageName { get; set; }
    }

    class MainClass
    {
        static CommandArgs Command;

        static readonly Regex parametersPattern = new Regex(@"(,\s*)?(([\w\.\[\]]+) (\w+))");
        static readonly string transformOverloadedMethodFormat = @"  <attr path=""/api/package[@name='{6}']/class[@name='{0}']/method[@name='{1}' and count(parameter)={4}][{5}]/parameter[position()={3}]"" name=""name"">{2}</attr>";
        static Dictionary<string, int> KnownMethods;

        public static void Main (string[] args)
        {
            IModelBindingDefinition<CommandArgs> config = null;
            String errorMessage = null;
            var showHelp = args.Length == 0;

            try {
                config = Configuration.Configure<CommandArgs>();
                Command = config.CreateAndBind(args);
            } catch (InvalidOperationException ex) {
                showHelp = true;
                errorMessage = "Error: " + ex.Message;
            }

            if (showHelp)
            {
                var helpProvider = new HelpProvider();
                var help = helpProvider.GenerateModelHelp(config);
                Console.WriteLine(help.HelpText);
                Console.WriteLine(errorMessage);
                return;
            } else {
                Console.WriteLine(@"  <!-- This tool isn't aware of inherited methods, and the binding generator will complain that the xpath isn't found. -->");
                Console.WriteLine(@"  <!-- These nodes will be ignored by the generator. -->");
            }

            var files = Directory.EnumerateFiles(Command.JavadocPath, "*.html", SearchOption.AllDirectories);
            // TODO: Optionally parse allclasses-noframe.html instead
            foreach (var file in files)
            {
                ParseFile(file); // TODO: write the attr nodes direct to metadata.xml.
            }
        }

        static void ParseFile (string documentPath)
        {
            var doc = new HtmlDocument();
            doc.Load(documentPath);

            var xdoc = doc.CreateNavigator();
            xdoc.MoveToFirstChild();

            KnownMethods = new Dictionary<string,int>();

            // Process constructors.
            var ctorTable = doc.DocumentNode.SelectNodes("//table[preceding-sibling::a[@name=\"constructor_summary\"]]/tr[position()>1]/td[@class=\"colOne\"]/code");
            if (ctorTable != null) {
                ParseTable (documentPath, ctorTable);
            }
            // Process methods.
            var methodTable = doc.DocumentNode.SelectNodes("//table[preceding-sibling::a[@name=\"method_summary\"]]/tr[position()>1]/td[@class=\"colLast\"]/code");
            if (methodTable != null) {
                ParseTable (documentPath, methodTable);
            }
        }

        static void ParseTable (string documentPath, HtmlNodeCollection methodRows)
        {
            var fileInfo = new FileInfo (documentPath);
            var className = fileInfo.Name.Replace (fileInfo.Extension, String.Empty);
            foreach (var row in methodRows) {
                ParseMethodString (KnownMethods, className, row.InnerText.Replace ("&nbsp;", " ").Replace (Environment.NewLine, " "));
            }
        }

        static string[] ParseMethodString (Dictionary<string, int> knownMethods, string className, string methodString)
        {

            if (!parametersPattern.IsMatch(methodString)) {
                return null;
            }

            var methodName = methodString.Split(new[] { '(' }, StringSplitOptions.None)[0];

            var matches = parametersPattern.Matches(methodString);

            var methodKey = methodName + matches.Count;
            knownMethods[methodKey] = knownMethods.ContainsKey(methodKey) ? knownMethods[methodKey]+ 1 : 1;

            var paramPosition = 1;
            var results = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++) {
                Match match = matches [i];
                results[i] = String.Format(transformOverloadedMethodFormat, className, methodName, match.Groups[4], paramPosition, matches.Count, knownMethods[methodKey], Command.PackageName);
                Console.WriteLine(results[i]);
                paramPosition++;
            }

            return results;
        }
    }
}
