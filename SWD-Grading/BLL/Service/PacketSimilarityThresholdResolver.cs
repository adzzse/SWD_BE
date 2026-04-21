using BLL.Interface;
using BLL.Model.Config;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL.Service
{
    public class PacketSimilarityThresholdResolver : IPacketSimilarityThresholdResolver
    {
        private readonly PacketSimilarityOptions _options;

        public PacketSimilarityThresholdResolver(IOptions<PacketSimilarityOptions> options)
        {
            _options = options.Value;
        }

        public decimal ResolveThreshold(int? examId, int? questionNumber, string? scope, decimal? requestThreshold)
        {
            if (requestThreshold.HasValue)
            {
                return NormalizeThreshold(requestThreshold.Value);
            }

            var normalizedScope = NormalizeScope(scope);

            var rules = _options.QuestionThresholds ?? new List<QuestionThresholdOption>();
            var matchedQuestionRule = rules
                .Where(x => IsRuleMatch(x, examId, questionNumber, normalizedScope))
                .OrderByDescending(GetRuleSpecificity)
                .FirstOrDefault();

            if (matchedQuestionRule != null)
            {
                return NormalizeThreshold(matchedQuestionRule.Threshold);
            }

            if (string.Equals(normalizedScope, "Global", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeThreshold(_options.GlobalThreshold);
            }

            if (string.Equals(normalizedScope, "SameQuestion", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeThreshold(_options.SameQuestionThreshold);
            }

            return NormalizeThreshold(_options.DefaultThreshold);
        }

        private static bool IsRuleMatch(QuestionThresholdOption rule, int? examId, int? questionNumber, string normalizedScope)
        {
            var ruleScope = NormalizeScope(rule.Scope);
            return string.Equals(ruleScope, normalizedScope, StringComparison.OrdinalIgnoreCase)
                   && (!rule.ExamId.HasValue || rule.ExamId == examId)
                   && (!rule.QuestionNumber.HasValue || rule.QuestionNumber == questionNumber);
        }

        private static int GetRuleSpecificity(QuestionThresholdOption rule)
        {
            var specificity = 0;
            if (rule.ExamId.HasValue)
            {
                specificity += 2;
            }

            if (rule.QuestionNumber.HasValue)
            {
                specificity += 1;
            }

            return specificity;
        }

        private static string NormalizeScope(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return "SameQuestion";
            }

            var normalized = scope.Trim();
            if (normalized.Equals("same_question", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("same-question", StringComparison.OrdinalIgnoreCase))
            {
                return "SameQuestion";
            }

            if (normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return "Global";
            }

            return normalized;
        }

        private static decimal NormalizeThreshold(decimal threshold)
        {
            if (threshold < 0m) return 0m;
            if (threshold > 1m) return 1m;
            return threshold;
        }
    }
}
