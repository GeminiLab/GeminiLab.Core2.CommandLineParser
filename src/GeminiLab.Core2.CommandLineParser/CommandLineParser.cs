﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeminiLab.Core2.CommandLineParser.Default;
using GeminiLab.Core2.CommandLineParser.Custom;

namespace GeminiLab.Core2.CommandLineParser {
    public class CommandLineParser<T> where T : new() {
        private class CategoryConfig {
            public Type    CategoryType  { get; set; }
            public Type    AttributeType { get; set; }
            public Type?   ConfigType    { get; set; }
            public object? Config        { get; set; }

            public IOptionCategoryBase Instance { get; set; }
            public List<OptionInDest>  Options  { get; set; }

            public CategoryConfig(Type categoryType, Type attributeType, Type? configType, object? config) {
                CategoryType = categoryType;
                AttributeType = attributeType;
                ConfigType = configType;
                Config = config;

                Instance = null!;
                Options = null!;
            }
        }

        private class OptionInDest {
            public OptionInDest(Attribute attribute, Type actualType, Type attributeType, MemberInfo target) {
                Attribute = attribute;
                ActualType = actualType;
                AttributeType = attributeType;
                Target = target;
            }

            public Attribute  Attribute     { get; set; }
            public Type       ActualType    { get; set; }
            public Type       AttributeType { get; set; }
            public MemberInfo Target        { get; set; }
        }


        private readonly Dictionary<Type, CategoryConfig> _configByCategoryType  = new Dictionary<Type, CategoryConfig>();
        private readonly Dictionary<Type, CategoryConfig> _configByAttributeType = new Dictionary<Type, CategoryConfig>();
        private readonly List<Type>                       _orderedCategoryTypes  = new List<Type>();

        private bool                      _evaluated = false;
        private List<IOptionCategoryBase> _categories;

        private void RemoveExistingConfigs(Type categoryType, Type attributeType) {
            CategoryConfig config;
            if (_configByCategoryType.TryGetValue(categoryType, out config)) {
                _orderedCategoryTypes.Remove(categoryType);
                _configByCategoryType.Remove(categoryType);
                _configByAttributeType.Remove(config.AttributeType);
            }

            if (_configByAttributeType.TryGetValue(attributeType, out config)) {
                _orderedCategoryTypes.Remove(config.CategoryType);
                _configByCategoryType.Remove(config.CategoryType);
                _configByAttributeType.Remove(attributeType);
            }
        }

        public CommandLineParser<T> Use<TOptionCategory, TOptionAttribute>()
            where TOptionCategory : IOptionCategory<TOptionAttribute>, new()
            where TOptionAttribute : OptionAttribute {
            _evaluated = false;

            var attributeType = typeof(TOptionAttribute);
            var categoryType = typeof(TOptionCategory);

            RemoveExistingConfigs(categoryType, attributeType);

            var categoryConfig = new CategoryConfig(categoryType, attributeType, null, null);

            _configByCategoryType[categoryType] = categoryConfig;
            _configByAttributeType[attributeType] = categoryConfig;
            _orderedCategoryTypes.Add(categoryType);

            return this;
        }


        public CommandLineParser<T> Use<TOptionCategory, TOptionAttribute, TConfig>(TConfig config)
            where TOptionCategory : IOptionCategory<TOptionAttribute>, IConfigurable<TConfig>, new()
            where TOptionAttribute : OptionAttribute {
            _evaluated = false;

            var attributeType = typeof(TOptionAttribute);
            var categoryType = typeof(TOptionCategory);
            var configType = typeof(TConfig);

            RemoveExistingConfigs(categoryType, attributeType);

            var categoryConfig = new CategoryConfig(categoryType, attributeType, configType, config);

            _configByCategoryType[categoryType] = categoryConfig;
            _configByAttributeType[attributeType] = categoryConfig;
            _orderedCategoryTypes.Add(categoryType);

            return this;
        }
        
        private IList<OptionInDest> ReadOptionsFromMemberInfos(IEnumerable<MemberInfo> memberInfos) {
            var options = new List<OptionInDest>();

            foreach (var memberInfo in memberInfos) {
                var attrs = memberInfo.GetCustomAttributes(typeof(OptionAttribute)).ToArray();
                foreach (var attr in attrs) {
                    var type = attr.GetType();
                    var actualType = type;

                    while (type != null && type != typeof(OptionAttribute)) {
                        if (_configByAttributeType.ContainsKey(type)) {
                            options.Add(new OptionInDest(attr, actualType, type, memberInfo));

                            break;
                        }

                        type = type.BaseType;
                    }
                }
            }

            return options;
        }

        private IList<OptionInDest> ReadOptions() {
            var options = new List<OptionInDest>();

            var typeOfT = typeof(T);
            options.AddRange(ReadOptionsFromMemberInfos(typeOfT.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)));
            options.AddRange(ReadOptionsFromMemberInfos(typeOfT.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)));
            options.AddRange(ReadOptionsFromMemberInfos(typeOfT.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)));

            return options;
        }

        private void EvaluateCategories(IList<OptionInDest> options) {
            foreach (var (categoryType, categoryConfig) in _configByCategoryType) {
                var instance = (IOptionCategoryBase) Activator.CreateInstance(categoryType);

                categoryConfig.Instance = instance;
                categoryConfig.Options = new List<OptionInDest>();

                if (categoryConfig.ConfigType != null) {
                    typeof(IConfigurable<>).MakeGenericType(categoryConfig.ConfigType).GetMethod(nameof(IConfigurable<int>.Config))!.Invoke(instance, new[] { categoryConfig.Config });
                }
            }

            foreach (var option in options) {
                _configByAttributeType[option.AttributeType].Options.Add(option);
            }

            foreach (var (categoryType, categoryConfig) in _configByCategoryType) {
                var optionType = typeof(IOptionCategory<>.Option).MakeGenericType(categoryConfig.AttributeType);
                var optionCtor = optionType.GetConstructor(new[] { categoryConfig.AttributeType, typeof(MemberInfo) });

                var listType = typeof(List<>).MakeGenericType(optionType);
                var listAdder = listType.GetMethod(nameof(List<int>.Add));

                var optionList = listType.GetConstructor(Array.Empty<Type>())!.Invoke(null);

                foreach (var option in categoryConfig.Options) {
                    listAdder!.Invoke(optionList, new[] { optionCtor!.Invoke(new object[] { option.Attribute, option.Target }) });
                }

                categoryType.GetProperty(nameof(IOptionCategory<OptionAttribute>.Options))!.GetSetMethod()!.Invoke(categoryConfig.Instance, new[] { optionList });
            }

            _categories = _orderedCategoryTypes.Select(type => _configByCategoryType[type].Instance).ToList();
        }

        private void EvaluateMetaInfo() {
            _evaluated = true;

            EvaluateCategories(ReadOptions());
        }

        private T DoParse(Span<string> args) {
            if (!_evaluated) EvaluateMetaInfo();

            int len = args.Length;
            int ptr = 0;
            var rv = new T();

            while (ptr < len) {
                var current = args[ptr..];
                int consumed = 0;

                foreach (var cat in _categories) {
                    consumed = cat.TryConsume(current, rv);

                    if (consumed > 0) {
                        break;
                    }
                }

                if (consumed <= 0) {
                    // todo: unknown option exception 
                    throw new FoobarException();
                }
                
                ptr += consumed;
            }

            return rv;
        }
        
        public T ParseFromSpan(ReadOnlySpan<string> args) {
            return DoParse(args.ToArray());
        }

        public T Parse(params string[] args) {
            return DoParse((string[])args.Clone());
        }

        private void LoadDefaultConfigs() {
            Use<ShortOptionCategory, ShortOptionAttribute, ShortOptionConfig>(new ShortOptionConfig { Prefix = "-" });
            Use<LongOptionCategory, LongOptionAttribute, LongOptionConfig>(new LongOptionConfig { Prefix = "--", ParameterSeparator = "=" });
            Use<TailArgumentsCategory, TailArgumentsAttribute, TailArgumentsConfig>(new TailArgumentsConfig { TailMark = "--" });
            Use<NonOptionArgumentCategory, NonOptionArgumentAttribute>();
        }

        public CommandLineParser() : this(false) { }

        public CommandLineParser(bool disableDefaultConfigs) {
            if (!disableDefaultConfigs) LoadDefaultConfigs(); 
            _categories = null!;
        }
    }
}
