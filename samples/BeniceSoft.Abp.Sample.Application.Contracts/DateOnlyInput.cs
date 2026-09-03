using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeniceSoft.Abp.Sample.Application.Contracts
{
    public class DateOnlyInput
    {
        public DateOnly Date { get; set; }

        public string Teststring { get; set; } = string.Empty;
    }
}
