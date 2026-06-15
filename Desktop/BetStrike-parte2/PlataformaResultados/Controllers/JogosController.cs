using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PlataformaResultados.Models;
using System.Net.Http.Json;

namespace PlataformaResultados.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JogosController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _betStrikeApiUrl;

        public JogosController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _connectionString = configuration.GetConnectionString("ResultadosConnection");
            _httpClientFactory = httpClientFactory;
            _betStrikeApiUrl = configuration["BetStrikeAPI:BaseUrl"] ?? configuration["BetStrikeAPI__BaseUrl"] ?? "http://betstrikeapi:8080";
        }

        // 1. INSERIR JOGO
        [HttpPost("inserir")]
        public async Task<IActionResult> InserirJogo([FromBody] Jogo novoJogo)
        {
            if (string.IsNullOrWhiteSpace(novoJogo.Codigo))
                return BadRequest("O código do jogo é obrigatório.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(novoJogo.Codigo, @"^FUT-\d{4}-\d{4}$"))
                return BadRequest("Formato de código inválido. Use FUT-AAAA-JJNN.");

            // 1. Inserir na BD Resultados
            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("SP_InserirJogo", con))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Codigo", novoJogo.Codigo);
                    cmd.Parameters.AddWithValue("@Data", novoJogo.Data);
                    cmd.Parameters.AddWithValue("@Hora", novoJogo.Hora);
                    cmd.Parameters.AddWithValue("@Casa", novoJogo.EquipaCasa);
                    cmd.Parameters.AddWithValue("@Fora", novoJogo.EquipaFora);
                    cmd.Parameters.AddWithValue("@Comp", (object)novoJogo.Competicao ?? DBNull.Value);
                    con.Open();
                    cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Erro ao inserir em Resultados: " + ex.Message);
            }

            // 2. Sincronizar com BetStrikeAPI → BD Apostas
            string msgIntegracao;
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.PostAsJsonAsync($"{_betStrikeApiUrl}/api/jogos/inserir", novoJogo);
                msgIntegracao = response.IsSuccessStatusCode
                    ? "Sincronizado com BD Apostas."
                    : $"BetStrikeAPI respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
            }
            catch (Exception ex)
            {
                // Se chegar aqui, a BetStrikeAPI não está acessível na porta 53386
                // Verifica se o BetStrikeAPI_1 está a correr com: dotnet run
                msgIntegracao = $"FALHA ao contactar BetStrikeAPI (porta 53386): {ex.Message}";
            }

            return CreatedAtAction(nameof(ObterJogo),
                new { codigo = novoJogo.Codigo },
                new { Codigo = novoJogo.Codigo, Mensagem = "Jogo criado em Resultados. " + msgIntegracao });
        }

        // 2. ATUALIZAR JOGO
        [HttpPut("atualizar")]
        public async Task<IActionResult> AtualizarJogo([FromBody] AtualizacaoJogo dados)
        {
            // 1. Atualizar na BD Resultados
            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("SP_AtualizarJogo", con))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Codigo", dados.Codigo);
                    cmd.Parameters.AddWithValue("@NovoEstado", dados.Estado);
                    cmd.Parameters.AddWithValue("@GolosCasa", (object)dados.GolosCasa ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GolosFora", (object)dados.GolosFora ?? DBNull.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Erro ao atualizar em Resultados: " + ex.Message);
            }

            // 2. Sincronizar com BetStrikeAPI → BD Apostas
            string msgIntegracao;
            try
            {
                var payload = new
                {
                    Codigo = dados.Codigo,
                    Estado = dados.Estado,
                    GolosCasa = dados.GolosCasa,
                    GolosFora = dados.GolosFora
                };
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.PostAsJsonAsync($"{_betStrikeApiUrl}/api/jogos/atualizar", payload);
                msgIntegracao = response.IsSuccessStatusCode
                    ? "Sincronizado com BD Apostas."
                    : $"BetStrikeAPI respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
            }
            catch (Exception ex)
            {
                msgIntegracao = $"FALHA ao contactar BetStrikeAPI (porta 53386): {ex.Message}";
            }

            return Ok($"Jogo {dados.Codigo} atualizado. {msgIntegracao}");
        }

        // 3. LISTAR JOGOS
        [HttpGet("listar")]
        public IActionResult ListarJogos([FromQuery] DateTime? data = null, [FromQuery] int? estado = null)
        {
            var lista = new List<object>();
            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("SP_ListarJogos", con))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Data", (object)data ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Estado", (object)estado ?? DBNull.Value);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                Id = reader["ID"],
                                Codigo = reader["Codigo_Jogo"],
                                EquipaCasa = reader["Equipa_Casa"],
                                EquipaFora = reader["Equipa_Fora"],
                                Estado = reader["Estado"]
                            });
                        }
                    }
                }
                return Ok(lista);
            }
            catch (Exception ex) { return BadRequest("Erro ao listar: " + ex.Message); }
        }

        // 4. OBTER JOGO ESPECÍFICO
        [HttpGet("{codigo}")]
        public IActionResult ObterJogo(string codigo)
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            using (SqlCommand cmd = new SqlCommand("SP_DetalheJogo", con))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Codigo", codigo);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        return Ok(new { Codigo = reader["Codigo_Jogo"], Estado = reader["Estado"] });
                }
            }
            return NotFound($"Jogo {codigo} não encontrado.");
        }

        // 5. REMOVER JOGO
        [HttpDelete("remover/{codigo}")]
        public IActionResult RemoverJogo(string codigo)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("SP_RemoverJogo", con))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Codigo", codigo);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                return Ok($"Jogo {codigo} removido.");
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}