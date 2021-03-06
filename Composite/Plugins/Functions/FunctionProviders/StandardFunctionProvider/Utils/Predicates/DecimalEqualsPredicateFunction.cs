﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlTypes;

using Composite.Functions;

using Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Foundation;
using Composite.Core.ResourceSystem;
using System.Linq.Expressions;

namespace Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Utils.Predicates
{
    internal sealed class DecimalEqualsPredicateFunction : StandardFunctionBase
    {
        public DecimalEqualsPredicateFunction(EntityTokenFactory entityTokenFactory)
            : base("DecimalEquals", "Composite.Utils.Predicates", typeof(Expression<Func<decimal, bool>>), entityTokenFactory)
        {
        }


        protected override IEnumerable<StandardFunctionParameterProfile> StandardFunctionParameterProfiles
        {
            get
            {
                WidgetFunctionProvider widget = StandardWidgetFunctions.DecimalTextBoxWidget;

                yield return new StandardFunctionParameterProfile(
                    "Value", typeof(decimal), true, new NoValueValueProvider(), widget);
            }
        }


        public override object Execute(ParameterList parameters, FunctionContextContainer context)
        {
            decimal value = parameters.GetParameter<decimal>("Value");
            Expression<Func<decimal,bool>> predicate = f=>f == value;
            return predicate;
        }
    }
}
