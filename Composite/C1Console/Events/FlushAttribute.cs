﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Composite.C1Console.Events
{
    /// <summary>
    /// This attribute registres a method that can be called to make a local flush
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    [AttributeUsage(AttributeTargets.Class)]
    public class FlushAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="methodName">The name of the method to call when doing a local flush. Must be of type: static void() </param>
        public FlushAttribute(string methodName)
        {
            this.MethodName = methodName;            
        }


        /// <exclude />
        public string MethodName { get; private set; }
    }
}
