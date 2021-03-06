using System;
using System.Collections.Generic;
using GeminiLab.Core2.CommandLineParser;
using GeminiLab.Core2.CommandLineParser.Custom;
using GeminiLab.Core2.CommandLineParser.Default;
using Xunit;

namespace XUnitTester {
    public class DefaultStyleTestOptions {
        public Queue<string> Logs { get; } = new Queue<string>();

        [ShortOption('a'), LongOption("alpha"), ParameterRequired]
        public string OptionA {
            set => Logs.Enqueue($"A:{value}");
        }

        [ShortOption('b'), Switch]
        private bool OptionB {
            set => Logs.Enqueue($"B:{value}");
        }

        [LongOption("bravo"), Switch]
        public void OptionBPlus() => Logs.Enqueue($"B:{true}");

        [ShortOption('c'), ParameterRequired]
        private void OptionC(string value) => Logs.Enqueue($"C:{value}");

        [ShortOption('d'), LongOption("delta"), ParameterOptional]
        public string? OptionD {
            set => Logs.Enqueue($"D:{value ?? "default"}");
        }

        [ShortOption('e'), LongOption("echo"), ParameterRequired]
        private string OptionE {
            set => Logs.Enqueue($"E:{value ?? "default"}");
        }

        [ShortOption('f'), LongOption("foxtrot")]
        private string? OptionF {
            set => Logs.Enqueue($"F:{value ?? "default"}");
        }

        [NonOptionArgument]
        public void NonOptionArgument(string value) => Logs.Enqueue($"NOA:{value}");

        [TailArguments]
        public void TailArguments(IEnumerable<string> value) {
            foreach (var str in value) {
                Logs.Enqueue($"TAIL:{str}");
            }
        }
    }

    public class DefaultStyleTestOptionB {
        public Queue<string> Logs { get; } = new Queue<string>();

        [ShortOption('a'), LongOption("alpha")]
        public string OptionA {
            set => Logs.Enqueue($"A:{value}");
        }

        [ShortOption('x'), LongOption("x-ray")]
        public bool OptionX {
            set => Logs.Enqueue($"X:{value}");
        }

        [UnknownOptionHandler]
        public ExceptionHandlerResult OnUnknownOption(UnknownOptionException exception) {
            Logs.Enqueue($"UNKNOWN:{exception.Option}");
            return ExceptionHandlerResult.ContinueParsing;
        }
    }

    public class DefaultStyleTestOptionC {
        [UnknownOptionHandler]
        public ExceptionHandlerResult OnUnknownOption(UnknownOptionException exception) {
            return ExceptionHandlerResult.CallNextHandler;
        }
    }

    public static class DefaultStyle {
        private static void AssertLogQueue(Queue<string> logs, params string[] expected) {
            foreach (var s in expected) {
                Assert.NotEmpty(logs);
                Assert.Equal(s, logs.Dequeue());
            }

            Assert.Empty(logs);
        }

        [Fact]
        public static void Normal() {
            var args = new[] { "-ax", "-bc", "charlie", "--bravo", "-d", "-dd", "--delta=d", "-e", "echo", "--echo", "echo", "--echo=echo", "echo", "-f", "--", "-ax", "bravo", };
            var result = new CommandLineParser<DefaultStyleTestOptions>().Parse(args);
            AssertLogQueue(result.Logs, "A:x", "B:True", "C:charlie", "B:True", "D:default", "D:d", "D:d", "E:echo", "E:echo", "E:echo", "NOA:echo", "F:default", "TAIL:-ax", "TAIL:bravo");
        }

        [Fact]
        public static void Error() {
            var parser = new CommandLineParser<DefaultStyleTestOptionB>();

            AssertLogQueue(parser.Parse("-ax", "--", "-c").Logs, "A:x", "UNKNOWN:--", "UNKNOWN:-c");
            AssertLogQueue(parser.Parse("-ax", "-b", "-c").Logs, "A:x", "UNKNOWN:-b", "UNKNOWN:-c");
            AssertLogQueue(parser.Parse("-xz").Logs, "X:True", "UNKNOWN:z");
            AssertLogQueue(parser.Parse("--alpha=1", "--bravo").Logs, "A:1", "UNKNOWN:--bravo");

            Assert.ThrowsAny<Exception>(() => { CommandLineParser<DefaultStyleTestOptionB>.DoParse("-a"); });
            Assert.ThrowsAny<Exception>(() => { CommandLineParser<DefaultStyleTestOptionB>.DoParse(new[] { "--alpha" }.AsSpan()); });

            var parserB = new CommandLineParser<DefaultStyleTestOptionC>(false)
                .Use<UnknownOptionHandlerComponent>();

            Assert.Throws<UnknownOptionException>(() => { parserB.Parse("-a"); });
            Assert.Throws<UnknownOptionException>(() => { parserB.Parse("--alpha"); });
        }
    }
}
