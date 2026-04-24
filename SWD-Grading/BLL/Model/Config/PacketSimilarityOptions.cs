using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Model.Config
{
    public class PacketSimilarityOptions
    {
        public decimal DefaultThreshold { get; set; } = 0.80m;
        public decimal SameQuestionThreshold { get; set; } = 0.80m;
        public decimal GlobalThreshold { get; set; } = 0.90m;

        public bool SeedTestDataOnStartup { get; set; } = false;
        public int? SeedExamId { get; set; }

        public List<QuestionThresholdOption> QuestionThresholds { get; set; } = new();
    }

    public class QuestionThresholdOption
    {
        public int? ExamId { get; set; }
        public int? QuestionNumber { get; set; }
        public string Scope { get; set; } = "SameQuestion";
        public decimal Threshold { get; set; }
    }
}
