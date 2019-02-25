using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace Mayflower.CustomValidation
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class PhoneFormatAttribute : ValidationAttribute, IClientValidatable
    {
        public override bool IsValid(object value)
        {
            if (value != null)
            {
                if (!Regex.IsMatch(value.ToString(), @"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$"))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(ModelMetadata metadata, ControllerContext context)
        {
            //string errorMessage = this.FormatErrorMessage(metadata.DisplayName);
            string errorMessage = ErrorMessageString;

            // The value we set here are needed by the jQuery adapter
            ModelClientValidationRule intTelInputRule = new ModelClientValidationRule();
            intTelInputRule.ErrorMessage = errorMessage;
            intTelInputRule.ValidationType = "phoneformat"; // This is the name the jQuery adapter will use
            //"otherpropertyname" is the name of the jQuery parameter for the adapter, must be LOWERCASE!
            //intTelInputRule.ValidationParameters.Add("otherpropertyname", otherPropertyName);

            yield return intTelInputRule;
        }
    }
}