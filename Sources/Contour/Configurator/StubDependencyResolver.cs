namespace Contour.Configurator
{
    using System;

    /// <summary>
    ///   ������-�������� ��� ��������� ������������ ��� ���������� ������� ���� ���������. ������ ���������� ��� �����
    ///   �������������.
    /// </summary>
    public class StubDependencyResolver : IDependencyResolver
    {
        #region Public Methods and Operators

        /// <summary>
        /// The resolve.
        /// </summary>
        /// <param name="name">
        /// The name.
        /// </param>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        public object Resolve(string name, Type type)
        {
            return new InvalidOperationException("No IDependencyResolver provided.");
        }

        #endregion
    }
}
