using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EventBus;

namespace BeniceSoft.Abp.Sample.Application;

[EventName("BeniceSoft.Abp.Sample.SampleDtmHttpEvent")]
public class SampleDtmHttpEvent
{
    public string TestId { get; set; } = "";
}
