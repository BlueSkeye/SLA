using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla
{
    internal interface IBiDirEnumerator<T> : IEnumerator<T>
    {
        bool IsAfterLast { get; }

        bool IsBeforeFirst { get; }

        bool IsEnumeratingForward { get; }

        bool IsPositionValid { get; }

        void ReverseEnumDirection();
    }
}
