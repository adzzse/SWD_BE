using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Model.Response
{
    public class PacketSimilarityThresholdResponse
    {
        public int? ExamId { get; set; }
        public int? QuestionNumber { get; set; }
        public string Scope { get; set; } = "SameQuestion";
        public decimal EffectiveThreshold { get; set; }
    }
}
