using System.Collections.Generic;
using System.Linq;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core.SpecificationTrady
{
    public class EmaSpecification : Specification<IList<IOhlcv>>
    {
        private readonly int periodCount;
        private readonly decimal value;

        public EmaSpecification(int periodCount, decimal value)
        {
            this.periodCount = periodCount;
            this.value = value;
        }

        public override bool IsSatisfiedBy(IList<IOhlcv> candidate)
        {
            //  new EmaSpecification(14, 70).And(new EmaSpecification(14, 50));
            return candidate.Rsi(periodCount).Last().Tick.Value > value;
        }
    }
}
