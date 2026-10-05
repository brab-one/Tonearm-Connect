using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.TonearmConnect
{
    public class TonearmConnectSettingsValidator : AbstractValidator<TonearmConnectSettings>
    {
    }

    public class TonearmConnectSettings : IProviderConfig
    {
        private static readonly TonearmConnectSettingsValidator Validator = new TonearmConnectSettingsValidator();

        /// <summary>
        /// Carries a device's state or command in an action request's body (the query string is too
        /// small for a queue). Never saved: Tonearm calls the action without adding the connection.
        /// </summary>
        [FieldDefinition(0, Label = "Payload", Hidden = HiddenType.Hidden)]
        public string Payload { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
