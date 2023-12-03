using Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ProcessingModule
{
    /// <summary>
    /// Class containing logic for automated work.
    /// </summary>
    public class AutomationManager : IAutomationManager, IDisposable
    {
        private Thread automationWorker;
        private AutoResetEvent automationTrigger;
        private IStorage storage;
        private IProcessingManager processingManager;
        private int delayBetweenCommands;
        private IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationManager"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <param name="processingManager">The processing manager.</param>
        /// <param name="automationTrigger">The automation trigger.</param>
        /// <param name="configuration">The configuration.</param>
        public AutomationManager(IStorage storage, IProcessingManager processingManager, AutoResetEvent automationTrigger, IConfiguration configuration)
        {
            this.storage = storage;
            this.processingManager = processingManager;
            this.configuration = configuration;
            this.automationTrigger = automationTrigger;
            this.delayBetweenCommands = configuration.DelayBetweenCommands;
        }

        /// <summary>
        /// Initializes and starts the threads.
        /// </summary>
		private void InitializeAndStartThreads()
        {
            InitializeAutomationWorkerThread();
            StartAutomationWorkerThread();
        }

        /// <summary>
        /// Initializes the automation worker thread.
        /// </summary>
		private void InitializeAutomationWorkerThread()
        {
            automationWorker = new Thread(AutomationWorker_DoWork);
            automationWorker.Name = "Aumation Thread";
        }

        /// <summary>
        /// Starts the automation worker thread.
        /// </summary>
		private void StartAutomationWorkerThread()
        {
            automationWorker.Start();
        }


        private void AutomationWorker_DoWork()
        {
            EGUConverter egu = new EGUConverter();

            PointIdentifier L = new PointIdentifier(PointType.ANALOG_OUTPUT, 1000);
            PointIdentifier STOP = new PointIdentifier(PointType.DIGITAL_OUTPUT, 2000);
            PointIdentifier V1 = new PointIdentifier(PointType.DIGITAL_OUTPUT, 2002);
            PointIdentifier P1 = new PointIdentifier(PointType.DIGITAL_OUTPUT, 2005);
            PointIdentifier P2 = new PointIdentifier(PointType.DIGITAL_OUTPUT, 2006);

            List<PointIdentifier> pointList = new List<PointIdentifier> { L, STOP, V1, P1, P2 };
            int nivo_vode, stop, stanje_ventila, stanje_prekidaca1, stanje_prekidaca2;
            int drainageLevel = 6000;

            while (!disposedValue)
            {
                List<IPoint> points = storage.GetPoints(pointList);

                nivo_vode = (int)egu.ConvertToEGU(points[0].ConfigItem.ScaleFactor, points[0].ConfigItem.Deviation, points[0].RawValue);
                stop = points[1].RawValue;
                stanje_ventila = points[2].RawValue;
                stanje_prekidaca1 = points[3].RawValue;
                stanje_prekidaca2 = points[4].RawValue;
                int inFlow;
                int outFlow;

                //Stop == 1 => prekidaci P1 = 0 i P2 = 0 (iskljuceni)
                if (stop == 1)
                {
                    processingManager.ExecuteWriteCommand(points[3].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, P1.Address, 0);
                    processingManager.ExecuteWriteCommand(points[4].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, P2.Address, 0);
                }
                //Stop == 0 => ventil V1 = 0 (zatvoren)
                if (stop == 0)
                {
                    processingManager.ExecuteWriteCommand(points[2].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, V1.Address, 0);
                }
                //kada su prekidaci iskljuceni nivo vode se ne menja
                if (stanje_prekidaca1 == 0 && stanje_prekidaca2 == 0)
                {
                    inFlow = 0;
                    nivo_vode += inFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }
                //kada je P2 == 1 nivo vode se povecava za 80l/s
                if (stanje_prekidaca1 == 0 && stanje_prekidaca2 == 1)
                {
                    inFlow = 80;
                    nivo_vode += inFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }
                //kada je P1 == 1 nivo vode se povecava za 160l/s
                if (stanje_prekidaca1 == 1 && stanje_prekidaca2 == 0)
                {
                    inFlow = 160;
                    nivo_vode += inFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }
                //kada su oba prekidaca ukljucena nivo vode se povecava za 240l/s
                if (stanje_prekidaca1 == 1 && stanje_prekidaca2 == 1)
                {
                    inFlow = 240;
                    nivo_vode += inFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }

                //kada nivo vode predje HighAlarm(3.5m), odnosno 10500 litara, aktivira se automatsko praznjenje bazena
                //Stop = 1, Ventil = 1, Prekidaci se automatski iskljucuju
                //outFlow je brzina praznjenja od 50l/s
                if (nivo_vode >= points[0].ConfigItem.HighLimit)
                {
                    outFlow = 50;
                    stop = 1;
                    stanje_ventila = 1;
                    nivo_vode -= outFlow;
                    processingManager.ExecuteWriteCommand(points[1].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, STOP.Address, stop);
                    processingManager.ExecuteWriteCommand(points[2].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, V1.Address, stanje_ventila);
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }

                //Ako je Ventil otvoren i visina vode veca od 2m (6000litara) bazen se prazni 50l/s
                if (stanje_ventila == 1 && nivo_vode > drainageLevel)
                {
                    outFlow = 50;
                    nivo_vode -= outFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }
                //ako nivo vode padne ispod drainageLevela onda zatvaramo ventil
                else if(stanje_ventila == 1 && nivo_vode <= drainageLevel)
                {
                    stanje_ventila = 0;
                    processingManager.ExecuteWriteCommand(points[2].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, V1.Address, stanje_ventila);
                }
                //Da bi mogli da prijavimo LowAlarm kad visina vode je manja od 1m
                //odnosno 3000 litara 
                if (nivo_vode <= drainageLevel && stanje_ventila == 1)
                {
                    outFlow = 50;
                    nivo_vode -= outFlow;
                    processingManager.ExecuteWriteCommand(points[0].ConfigItem, configuration.GetTransactionId(), configuration.UnitAddress, L.Address, nivo_vode);
                }

                automationTrigger.WaitOne();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls


        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">Indication if managed objects should be disposed.</param>
		protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void Start(int delayBetweenCommands)
        {
            this.delayBetweenCommands = delayBetweenCommands * 1000;
            InitializeAndStartThreads();
        }

        /// <inheritdoc />
        public void Stop()
        {
            Dispose();
        }
        #endregion
    }
}
