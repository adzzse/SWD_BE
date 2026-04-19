using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interface
{
    public interface IPacketSimilarityThresholdResolver
    {
        decimal ResolveThreshold(int? examId, int? questionNumber, string? scope, decimal? requestThreshold);
    }
}
