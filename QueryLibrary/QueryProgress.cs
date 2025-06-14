using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryLibrary
{
    public enum QueryProgress
    {
        NotStarted = 0,
        GatheringQueryInfo = 1,
        StartingQuery = 2,
        CreatingOutput = 3,
        Done = 4
    }
}
