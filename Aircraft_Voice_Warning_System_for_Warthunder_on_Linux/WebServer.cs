//AI disclaimer - this whole file is generated with AI

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Aircraft_Voice_Warning_System_for_Warthunder_on_Linux;

public static class WebServer
{
    private static Thread? _listenerThread;
    private static HttpListener? _listener;
    private static string _root;

    static WebServer()
    {
        try
        {
            _root = Path.Combine(AppContext.BaseDirectory, "WebUI", "wwwroot");
            _listenerThread = new Thread(StartListener) { IsBackground = true };
            _listenerThread.Start();
        }
        catch { }
    }

    public static void Start()
    {
        if (_listenerThread == null || !_listenerThread.IsAlive)
        {
            _root = Path.Combine(AppContext.BaseDirectory, "WebUI", "wwwroot");
            _listenerThread = new Thread(StartListener) { IsBackground = true };
            _listenerThread.Start();
        }
    }

    private static void StartListener()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:8112/");
            _listener.Start();
            while (_listener.IsListening)
            {
                var ctx = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
        }
        catch
        {
            // ignore startup errors
        }
    }

    private static void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            string urlPath = ctx.Request.Url.AbsolutePath;
            if (urlPath == "/" || urlPath == "") urlPath = "/index.html";

            if (urlPath.Equals("/telemetry", StringComparison.OrdinalIgnoreCase))
            {
                SendTelemetry(ctx);
                return;
            }

            string filePath = Path.Combine(_root, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
            {
                byte[] data = File.ReadAllBytes(filePath);
                ctx.Response.ContentType = GetContentType(filePath);
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.OutputStream.Write(data, 0, data.Length);
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            }
            else
            {
                ctx.Response.StatusCode = 404;
                using (var sw = new StreamWriter(ctx.Response.OutputStream))
                {
                    sw.Write("Not Found");
                }
            }
        }
        catch
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.OutputStream.Close(); } catch { }
        }
    }

    private static void SendTelemetry(HttpListenerContext ctx)
    {
        var state = Program.CurrentAircraftState; // Program exposes this as public static
        var dto = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            TAS_kmh = state?.TAS_kmh ?? 0.0f,
            Altitude_m = state?.Alititude_Above_Sea_Level ?? 0.0f,
            Aileron = state?.Aileron ?? 0.0f,
            Elevator = state?.Elevator ?? 0.0f,
            Rudder = state?.Rudder ?? 0.0f,
            AoA = state?.Angle_Of_Attack ?? 0.0f,
            G = state?.G_Force_On_Lift_Vector_mss ?? 0.0f,
            Flaps = state?.Flaps ?? 0.0f,
            Gear = state?.Gear ?? 0.0f
        };

        string json = JsonSerializer.Serialize(dto);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    private static string GetContentType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}
