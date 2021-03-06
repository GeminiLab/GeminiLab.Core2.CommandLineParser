using System.Collections.Generic;
using GeminiLab.Core2.CommandLineParser;
using GeminiLab.Core2.CommandLineParser.Custom;
using GeminiLab.Core2.CommandLineParser.Default;
using Xunit;

namespace XUnitTester {
    public class ClStyleTestOptions {
        [LongOption("OptionFirst")]
        public string OptA = "";

        [LongOption("S"), LongOption("OptionSecond")]
        public bool OptB = false;

        [TailArguments]
        public IEnumerable<string> TailArguments { get; set; } = null!;

        [UnknownOptionHandler]
        ExceptionHandlerResult OnUnknownOption(UnknownOptionException exception) {
            return ExceptionHandlerResult.GracefullyBreak;
        }
    }

    public static class ClStyle {
        private static void AssertLogQueue(Queue<string> logs, params string[] expected) {
            foreach (var s in expected) {
                Assert.NotEmpty(logs);
                Assert.Equal(s, logs.Dequeue());
            }

            Assert.Empty(logs);
        }

        [Fact]
        public static void NormalTest() {
            var args = new[] {
                "/OptionFirst:first",
                "/S",
                "/Unknown",
                "/OptionFirst:second",
            };

            var parser = new CommandLineParser<ClStyleTestOptions>(false)
                .Use<OptionComponent, OptionConfig>(new OptionConfig()) // test duplicated component loading here
                .Use<OptionComponent, OptionConfig>(new OptionConfig {
                    ShortPrefix = '\0',
                    LongPrefix = "/",
                    ParameterDelimiter = ":",
                })
                .Use<UnknownOptionHandlerComponent>()
                .Use<UnknownOptionHandlerComponent>();
            var result = parser.Parse(args);

            Assert.True(result.OptB);
            Assert.Equal("first", result.OptA);
        }
    }
}
