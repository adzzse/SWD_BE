using BLL.Interface;
using BLL.Model.Config;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            var matchedQuestionRule = _options.QuestionThresholds
                .FirstOrDefault(x =>
                    (!x.ExamId.HasValue || x.ExamId == examId) &&
                    (!x.QuestionNumber.HasValue || x.QuestionNumber == questionNumber) &&
                    string.Equals(NormalizeScope(x.Scope), normalizedScope, StringComparison.OrdinalIgnoreCase));

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

        private static string NormalizeScope(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return "SameQuestion";
            }

            return scope.Trim();
        }

        private static decimal NormalizeThreshold(decimal threshold)
        {
            if (threshold < 0m) return 0m;
            if (threshold > 1m) return 1m;
            return threshold;
        }
    }
}
