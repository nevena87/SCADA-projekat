using Common;

namespace ProcessingModule
{
    /// <summary>
    /// Class containing logic for alarm processing.
    /// </summary>
    public class AlarmProcessor
	{
        /// <summary>
        /// Processes the alarm for analog point.
        /// </summary>
        /// <param name="eguValue">The EGU value of the point.</param>
        /// <param name="configItem">The configuration item.</param>
        /// <returns>The alarm indication.</returns>
		public AlarmType GetAlarmForAnalogPoint(double eguValue, IConfigItem configItem)
		{
            if (CheckScope(eguValue, configItem))
            {
                if (CheckAlarm(eguValue, configItem) == 1)
                {
                    return AlarmType.HIGH_ALARM;
                }
                else if (CheckAlarm(eguValue, configItem) == 0)
                {
                    return AlarmType.NO_ALARM;
                }
                else
                {
                    return AlarmType.LOW_ALARM;
                }
            }
            else
            {
                return AlarmType.REASONABILITY_FAILURE;
            }
        }

        private static bool CheckScope(double eguValue, IConfigItem configItem)
        {
            if (eguValue <= configItem.EGU_Min || eguValue >= configItem.EGU_Max)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static int CheckAlarm(double eguValue, IConfigItem configItem)
        {
            if (eguValue >= configItem.HighLimit)
            {
                return 1;
            }
            else if (eguValue <= configItem.LowLimit)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Processes the alarm for digital point.
        /// </summary>
        /// <param name="state">The digital point state</param>
        /// <param name="configItem">The configuration item.</param>
        /// <returns>The alarm indication.</returns>
		public AlarmType GetAlarmForDigitalPoint(ushort state, IConfigItem configItem)
		{
            if (state == configItem.AbnormalValue)
            {
                return AlarmType.ABNORMAL_VALUE;
            }
            else
            {
                return AlarmType.NO_ALARM;
            }
        }
	}
}
