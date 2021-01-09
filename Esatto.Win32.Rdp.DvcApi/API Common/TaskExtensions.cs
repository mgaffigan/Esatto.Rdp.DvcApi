using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi
{
    internal static class TaskExtensions
    {
        public static T GetResultOrException<T>(this Task<T> task)
        {
            try
            {
                return task.Result;
            }
            catch (AggregateException ae) when (ae.InnerExceptions.Count == 1)
            {
                throw ae.InnerException;
            }
        }

        public static void WaitOrException(this Task task)
        {
            try
            {
                task.Wait();
            }
            catch (AggregateException ae) when (ae.InnerExceptions.Count == 1)
            {
                throw ae.InnerException;
            }
        }
    }
}
