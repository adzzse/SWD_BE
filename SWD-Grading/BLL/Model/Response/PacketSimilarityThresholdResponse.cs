using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Model.Response
{
    public class PacketSimilarityThresholdResponse
    {
        public long? ExamId { get; set; }
        public int? QuestionNumber { get; set; }
        public string Scope { get; set; } = "SameQuestion";
        public decimal? RequestThreshold { get; set; }
        public decimal EffectiveThreshold { get; set; }
    }
}
