using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Phone.UserData;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Helper to query system Calendar appointments.
    /// Based on Microsoft Tetra Lockscreen implementation.
    /// </summary>
    public static class CalendarHelper
    {
        public static Task<IEnumerable<Appointment>> GetTodayAppointmentsAsync(int limit = 5)
        {
            var tcs = new TaskCompletionSource<IEnumerable<Appointment>>();
            try
            {
                var appointments = new Appointments();
                DateTime start = DateTime.Today;
                DateTime end = DateTime.Today.AddDays(1);

                EventHandler<AppointmentsSearchEventArgs> handler = null;
                handler = (s, e) =>
                {
                    appointments.SearchCompleted -= handler;
                    tcs.TrySetResult(e.Results != null ? e.Results.Take(limit) : Enumerable.Empty<Appointment>());
                };

                appointments.SearchCompleted += handler;
                appointments.SearchAsync(start, end, limit * 2, "CalendarQuery");
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }
    }
}
