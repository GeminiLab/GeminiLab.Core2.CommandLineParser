using System.Collections.Generic;
using GeminiLab.Core2.CommandLineParser;
using GeminiLab.Core2.CommandLineParser.Default;
using Xunit;

namespace XUnitTester {
    public class LateConfig {
        public class TestOptions {
            [ShortOption('a')]
            public bool A { get; set; }

            [LongOption("bravo", OptionParameter.Required)]
            public string B { get; set; }

            [TailArguments]
            public IEnumerable<string> T { get; set; }
        }

        [Fact]
        public static void LateConfigTest() {
            var parser = new CommandLineParser<TestOptions>()
                .Config((object) new ShortOptionConfig { PrefixChar = '/' })
                .Config<LongOptionConfig>(new LongOptionConfig { Prefix = "-" })
                .Config<TailArgumentsCategory>((object) new TailArgumentsConfig { TailMark = "??" })
                ;

            var options = parser.Parse("/a", "-bravo=b", "??", "!!");

            Assert.True(options.A);
            Assert.Equal("b", options.B);
            Assert.Equal(new[] { "!!" }, options.T);
        }
    }
}