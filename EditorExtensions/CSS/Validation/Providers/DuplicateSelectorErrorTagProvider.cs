﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.CSS.Core;
using Microsoft.VisualStudio.Utilities;
using MadsKristensen.EditorExtensions.Settings;
using Microsoft.CSS.Core.TreeItems;
using Microsoft.CSS.Core.Checker;
using Microsoft.CSS.Core.Parser;

namespace MadsKristensen.EditorExtensions.Css
{
    [Export(typeof(ICssItemChecker))]
    [Name("DuplicateSelectorErrorTagProvider")]
    [Order(After = "Default Declaration")]
    internal class DuplicateSelectorErrorTagProvider : ICssItemChecker
    {
        public ItemCheckResult CheckItem(ParseItem item, ICssCheckerContext context)
        {
            if (!WESettings.Instance.Css.ValidateDuplicateSelectors)
                return ItemCheckResult.Continue;

            RuleSet rule = (RuleSet)item;

            if (!rule.IsValid || context == null)
                return ItemCheckResult.Continue;

            List<RuleResult> cache = context.GetState(this) as List<RuleResult>;

            if (cache == null || (cache.Count > 0 && cache[0].Rule.Parent != rule.Parent))
            {
                cache = BuildCache(rule);
                context.SetState(this, cache);
            }

            string ruleText = GetSelectorText(rule);
            int start = rule.Start;
            RuleResult dupe = null;
            for (int i = 0; i < cache.Count; i++)
            {
                if (cache[i].Start >= start)
                    break;

                if (ruleText == cache[i].Value && rule.Parent == cache[i].Rule.Parent)
                {
                    dupe = cache[i];
                    break;
                }
            }

            if (dupe != null)
            {
                SelectorErrorTag tag = new SelectorErrorTag(rule.Selectors, "The selector " + ruleText + " is already specified earlier.  Consider combining the two rule-sets.");
                context.AddError(tag);
            }

            return ItemCheckResult.Continue;
        }

        private static string GetSelectorText(RuleSet rule)
        {
            var selectorsText = rule.Selectors.OrderBy(s => s.Text.Trim(',')).Select(s => s.Text.Trim(','));
            return string.Join(", ", selectorsText);
        }

        public IEnumerable<Type> ItemTypes
        {
            get { return new[] { typeof(RuleSet) }; }
        }

        private static List<RuleResult> BuildCache(RuleSet rule)
        {
            var visitor = new CssItemCollector<RuleSet>();
            rule.Parent.Accept(visitor);
            List<RuleResult> list = new List<RuleResult>();

            foreach (RuleSet rs in visitor.Items)
            {
                RuleResult result = new RuleResult(rs, rs.Start, GetSelectorText(rs));
                list.Add(result);
            }

            return list;
        }

        private class RuleResult
        {
            public RuleResult(RuleSet rule, int start, string value)
            {
                Rule = rule;
                Start = start;
                Value = value;
            }

            public RuleSet Rule;
            public int Start;
            public string Value;
        }
    }
}
