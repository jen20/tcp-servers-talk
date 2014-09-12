using System;

namespace WithTcpConnection
{
    public class Eat
    {
        public static void Exception(Action mayThrow)
        {
            // ReSharper disable EmptyGeneralCatchClause
            try
            {
                mayThrow();
            }
            catch (Exception)
            {
            }
            // ReSharper restore EmptyGeneralCatchClause			
        }

        public static T Exception<T>(Func<T> mayThrow, T defaultValue = default(T))
        {
            if (mayThrow == null)
                throw new ArgumentNullException("mayThrow");

            // ReSharper disable EmptyGeneralCatchClause
            try
            {
                return mayThrow();
            }
            catch (Exception)
            {
                return defaultValue;
            }
            // ReSharper restore EmptyGeneralCatchClause
        }
    }
}