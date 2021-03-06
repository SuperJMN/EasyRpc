﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EasyRpc.AspNetCore.Middleware
{
    public class FactoryExposureConfiguration : IFactoryExposureConfiguration, IExposedMethodInformationProvider
    {
        private readonly string _path;
        private readonly ICurrentApiInformation _currentApiInformation;
        private readonly List<Tuple<string, Delegate, InvokeMethodWithArray>> _methods = new List<Tuple<string, Delegate, InvokeMethodWithArray>>();

        public FactoryExposureConfiguration(string path, ICurrentApiInformation currentApiInformation)
        {
            _path = path;
            _currentApiInformation = currentApiInformation;
        }

        public IFactoryExposureConfiguration Methods(Action<IFactoryMethodConfiguration> method)
        {
            method(new FactoryMethodConfiguration(
                (name, del, action) => _methods.Add(new Tuple<string, Delegate, InvokeMethodWithArray>(name, del, action))));

            return this;
        }

        public IEnumerable<IExposedMethodInformation> GetExposedMethods()
        {
            foreach (var methodTuple in _methods)
            {
                var methodInfo = methodTuple.Item2.GetMethodInfo();
                var filterOut = _currentApiInformation.MethodFilters.Any(func => !func(methodInfo));

                if (filterOut)
                {
                    continue;
                }

                var finalNames = new List<string>();

                if (_currentApiInformation.Prefixes.Count > 0)
                {
                    foreach (var prefixes in _currentApiInformation.Prefixes)
                    {
                        foreach (var prefix in prefixes(typeof(object)))
                        {
                            finalNames.Add(prefix + _path);
                        }
                    }

                }
                else
                {
                    finalNames.Add(_path);
                }

                var authorizations = new List<IMethodAuthorization>();

                foreach (var authorization in _currentApiInformation.Authorizations)
                {
                    foreach (var methodAuthorization in authorization(typeof(object)))
                    {
                        authorizations.Add(methodAuthorization);
                    }
                }

                var filters = new List<Func<ICallExecutionContext, IEnumerable<ICallFilter>>>();

                foreach (var func in _currentApiInformation.Filters)
                {
                    var filter = func(methodInfo);

                    if (filter != null)
                    {
                        filters.Add(filter);
                    }
                }

                BaseExposureConfiguration.ProcessAttributesOnMethod(methodInfo, authorizations, filters);

                var authArray = authorizations.Count > 0 ?
                    authorizations.ToArray() :
                    Array.Empty<IMethodAuthorization>();

                var filterArray = filters.Count > 0 ?
                    filters.ToArray() :
                    Array.Empty<Func<ICallExecutionContext, IEnumerable<ICallFilter>>>();

                yield return new FactoryExposedMethodInformation(typeof(object), 
                    finalNames, 
                    methodTuple.Item1, 
                    methodInfo, 
                    authArray, 
                    filterArray, 
                    methodTuple.Item3, 
                    null);
            }
        }
    }
}
