namespace BureauAdaptor
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly Settings _settings;

        private const Int32 DEFAULT_PORT = 10001, DEFAULT_NUM_CONNECTIONS = 20, DEFAULT_BUFFER_SIZE = Int16.MaxValue;

        private SocketListener? sl;

        public Worker(ILogger<Worker> logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Int32 port = _settings?.AdaptorSettings?.Port ?? DEFAULT_PORT;
                    Int32 numConnections = _settings?.AdaptorSettings?.MaxConnections ?? DEFAULT_NUM_CONNECTIONS;
                    Int32 bufferSize = DEFAULT_BUFFER_SIZE;
                    string[] args = Environment.GetCommandLineArgs();
                    string bureau = (args.Length > 1) ? args[1].Replace("-", "") : "TU";

                    sl = new SocketListener(numConnections, bufferSize, _logger, _settings, bureau);
                    sl.Start(port);
                    
                    _logger.LogInformation("Server listening on port {0}...", port);
                }
                catch (Exception)
                {
                    _logger.LogInformation("Failed to create socket at: {time}", DateTimeOffset.Now);
                }
            }
            else
                _logger.LogInformation("Service stopping. Stoppingtoken received at: {time}", DateTimeOffset.Now);
            /*
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
            */
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            sl.Stop();
            _logger.LogInformation("Service Stopped");
            await base.StopAsync(cancellationToken);
        }
    }
}