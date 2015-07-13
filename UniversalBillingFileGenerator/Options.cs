using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace UniversalBillingFileGenerator
{
    class Options
    {
        [Option('m', "months", Required=false, HelpText="How many months in the past")]
        public int Months { get; set; }

        [Option('f', "format", Required=false, HelpText="Data format (AST|EMA)")]
        public string Format { get; set; }

        [Option('t', "table", Required = false, HelpText="Data table (BillableItem|BillableItemEMA)")]
        public string Table { get; set; }

        [Option("nomail", Required = false, DefaultValue=false, HelpText = "Data table (BillableItem|BillableItemEMA)")]
        public bool NoMail { get; set; }
    }
}
