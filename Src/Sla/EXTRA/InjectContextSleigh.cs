﻿using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class InjectContextSleigh : InjectContext
    {
        public PcodeCacher cacher;
        public ParserContext pos;

        public InjectContextSleigh()
        {
            pos = (ParserContext*)0;
        }
        
        ~InjectContextSleigh()
        {
            if (pos != (ParserContext*)0)
                delete pos;
        }

        // We don't need this functionality for sleigh
        public override void encode(Encoder encoder) 
        {
        }
    }
}