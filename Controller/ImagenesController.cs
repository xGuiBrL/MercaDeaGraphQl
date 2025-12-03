using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Path = System.IO.Path;
using HotChocolate.Authorization;

namespace MercaDeaGraphQl.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagenesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ImagenesController(IWebHostEnvironment env)
        {
            _env = env;
        }
        [Authorize]

        [HttpPost("agregar")]
        public async Task<IActionResult> AgregarImagenes([FromForm] List<IFormFile> archivos)
        {
            if (archivos == null || archivos.Count == 0)
                return BadRequest("No se enviaron imágenes.");

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

            var carpeta = Path.Combine(webRoot, "uploads");
            if (!Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);

            var urls = new List<string>();

            foreach (var archivo in archivos)
            {
                var ext = Path.GetExtension(archivo.FileName).ToLower();
                var permitidos = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!permitidos.Contains(ext))
                    return BadRequest($"El archivo {archivo.FileName} no es una imagen válida.");

                if (archivo.Length > 5 * 1024 * 1024)
                    return BadRequest($"El archivo {archivo.FileName} es demasiado grande (máx 5MB).");

                var nuevoNombre = Guid.NewGuid().ToString() + ext;

                var ruta = Path.Combine(carpeta, nuevoNombre);

                using var stream = new FileStream(ruta, FileMode.Create);
                await archivo.CopyToAsync(stream);

                var url = $"{Request.Scheme}://{Request.Host}/uploads/{nuevoNombre}";
                urls.Add(url);
            }

            return Ok(new { imagenes = urls });
        }
        [Authorize]
        [HttpGet]
        public IActionResult ObtenerImagenes()
        {
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

            var carpeta = Path.Combine(webRoot, "uploads");
            if (!Directory.Exists(carpeta))
                return Ok(new { imagenes = Array.Empty<string>() });

            var archivos = Directory.GetFiles(carpeta)
                .Select(Path.GetFileName)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .Select(nombre => $"{Request.Scheme}://{Request.Host}/uploads/{nombre}")
                .ToList();

            return Ok(new { imagenes = archivos });
        }
    }
}
