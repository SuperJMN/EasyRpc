﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using EasyRpc.AspNetCore.Data;
using EasyRpc.AspNetCore.Messages;

namespace EasyRpc.AspNetCore.DataAnnotations.Impl
{
    public class DataAnnotationFilter : ICallExecuteFilter
    {
        private List<ValidationAttribute> _attributes;
        private int _index;
        private string _fieldName;

        public DataAnnotationFilter(List<ValidationAttribute> attributes, int index, string fieldName)
        {
            _attributes = attributes;
            _index = index;
            _fieldName = fieldName;
        }

        public void BeforeExecute(ICallExecutionContext context)
        {
            var validationValue = context.Parameters[_index];
            var validationErrorMessage = ImmutableLinkedList<string>.Empty;

            foreach (var validationAttribute in _attributes)
            {
                var validationContext = new ValidationContext(context.Instance, context.Context.RequestServices, null);

                var result = validationAttribute.GetValidationResult(validationValue, validationContext);

                if (result != ValidationResult.Success)
                {
                    validationErrorMessage = validationErrorMessage.Add(validationAttribute.FormatErrorMessage(_fieldName).Replace("field","parameter"));
                }
            }

            if (validationErrorMessage != ImmutableLinkedList<string>.Empty)
            {
                context.ContinueCall = false;

                var errorMessage = validationErrorMessage.Aggregate((v, c) => v + Environment.NewLine + c);

                if (context.ResponseMessage is ErrorResponseMessage currentResponse)
                {
                    errorMessage += currentResponse.Error;
                }

                context.ResponseMessage = new ErrorResponseMessage(context.RequestMessage.Version, context.RequestMessage.Id, JsonRpcErrorCode.InvalidRequest, errorMessage);
            }
        }

        public void AfterExecute(ICallExecutionContext context)
        {

        }
    }
}