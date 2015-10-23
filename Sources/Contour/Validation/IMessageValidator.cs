// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMessageValidator.cs" company="">
//   
// </copyright>
// <summary>
//   ��������� ���������.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Contour.Validation
{
    /// <summary>
    ///   ��������� ���������.
    /// </summary>
    public interface IMessageValidator
    {
        #region Public Methods and Operators

        /// <summary>
        /// ��������� ���������� ���������.
        /// </summary>
        /// <param name="message">
        /// ��������� ��� ��������.
        /// </param>
        /// <returns>
        /// ��������� ���������.
        /// </returns>
        ValidationResult Validate(IMessage message);

        #endregion
    }
}
