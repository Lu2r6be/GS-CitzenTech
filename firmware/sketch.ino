// Termômetro do ACS — firmware do ESP32 (Wokwi)
// O que faz: lê temperatura e umidade da casa (DHT22), manda para a API e acende o LED
// da cor que a API responder. O ESP32 NÃO decide a cor: ele só mede e mostra.

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <DHT.h>

// ======================= CONFIGURAÇÃO (mude só aqui) =======================
// Wokwi só alcança URL PÚBLICA: use o endereço do túnel (cloudflared) que aponta para a API.
const char* API_BASE   = "https://SEU-TUNEL.trycloudflare.com";
const char* DEVICE_ID  = "esp32-001";
const char* DEVICE_KEY = "chave-demo-do-esp32-001";  // chave DE DEMONSTRAÇÃO; a real vem do provisionamento
const unsigned long INTERVALO_MS = 10000;            // demo: 10 s. Em uso real: alguns minutos.

const char* WIFI_SSID  = "Wokwi-GUEST";              // rede aberta do simulador
const char* WIFI_SENHA = "";
const int   WIFI_CANAL = 6;                          // acelera a conexão no Wokwi
// ===========================================================================

const int PINO_DHT      = 15;
const int PINO_AZUL     = 25;
const int PINO_VERDE    = 26;
const int PINO_AMARELO  = 27;
const int PINO_VERMELHO = 14;

DHT dht(PINO_DHT, DHT22);
unsigned long ultimoEnvio = 0;

void apagarLeds() {
  digitalWrite(PINO_AZUL, LOW);
  digitalWrite(PINO_VERDE, LOW);
  digitalWrite(PINO_AMARELO, LOW);
  digitalWrite(PINO_VERMELHO, LOW);
}

// Acende só o LED do nível recebido. Nível desconhecido = tudo apagado (nunca "parece seguro").
void acenderNivel(const String& nivel) {
  apagarLeds();
  if (nivel == "azul")          digitalWrite(PINO_AZUL, HIGH);
  else if (nivel == "verde")    digitalWrite(PINO_VERDE, HIGH);
  else if (nivel == "amarelo")  digitalWrite(PINO_AMARELO, HIGH);
  else if (nivel == "vermelho") digitalWrite(PINO_VERMELHO, HIGH);
}

// Pisca todos os LEDs uma vez ao ligar: confere se a fiação está certa.
void testeDeLeds() {
  const int pinos[] = {PINO_AZUL, PINO_VERDE, PINO_AMARELO, PINO_VERMELHO};
  for (int pino : pinos) {
    digitalWrite(pino, HIGH);
    delay(250);
    digitalWrite(pino, LOW);
  }
}

void conectarWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  Serial.print("Conectando ao WiFi");
  WiFi.begin(WIFI_SSID, WIFI_SENHA, WIFI_CANAL);
  unsigned long inicio = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - inicio < 15000) {
    delay(300);
    Serial.print(".");
  }
  Serial.println(WiFi.status() == WL_CONNECTED ? " conectado" : " FALHOU");
}

// Faz uma requisição GET/POST. Devolve true se chegou resposta (qualquer status HTTP).
bool requisitar(bool post, const String& caminho, const String& corpo, int& status, String& resposta) {
  String url = String(API_BASE) + caminho;
  WiFiClientSecure seguro;
  WiFiClient simples;
  HTTPClient http;

  if (url.startsWith("https://")) {
    // setInsecure = não confere o certificado. Aceitável na demo (o túnel já usa HTTPS);
    // em produção, o certo é fixar o certificado da API.
    seguro.setInsecure();
    http.begin(seguro, url);
  } else {
    http.begin(simples, url);
  }
  http.setTimeout(8000);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Key", DEVICE_KEY);

  status = post ? http.POST(corpo) : http.GET();
  bool chegou = status > 0;
  resposta = chegou ? http.getString() : http.errorToString(status);
  http.end();
  return chegou;
}

// A API responde {"nivel":"amarelo"}. Procuramos o nome do nível no texto (sem biblioteca de JSON).
String extrairNivel(const String& json) {
  const char* niveis[] = {"azul", "verde", "amarelo", "vermelho"};
  for (const char* nivel : niveis) {
    if (json.indexOf(String("\"") + nivel + "\"") >= 0) return String(nivel);
  }
  return "";
}

// O ESP32 lê os limiares na inicialização, só para mostrar no monitor serial.
void lerLimiares() {
  int status;
  String resposta;
  if (requisitar(false, "/api/v1/config/limiares", "", status, resposta) && status == 200) {
    Serial.println("Limiares em vigor: " + resposta);
  } else {
    Serial.println("Não consegui ler os limiares (status " + String(status) + ")");
  }
}

void enviarLeitura(float temperatura, float umidade) {
  char corpo[128];
  snprintf(corpo, sizeof(corpo),
           "{\"deviceId\":\"%s\",\"temperaturaC\":%.1f,\"umidade\":%.1f}",
           DEVICE_ID, temperatura, umidade);

  int status;
  String resposta;
  if (!requisitar(true, "/api/v1/telemetria", String(corpo), status, resposta)) {
    apagarLeds();  // sem comunicação: apaga tudo (a API marca "sem sinal" depois de 30 min)
    Serial.println("Falha de rede: " + resposta);
    return;
  }

  String nivel = (status == 202) ? extrairNivel(resposta) : "";
  if (nivel == "") {
    apagarLeds();
    Serial.println("Resposta inesperada (HTTP " + String(status) + ")");
    return;
  }

  acenderNivel(nivel);
  Serial.printf("%.1f C | %.1f %% -> %s\n", temperatura, umidade, nivel.c_str());
}

void setup() {
  Serial.begin(115200);
  pinMode(PINO_AZUL, OUTPUT);
  pinMode(PINO_VERDE, OUTPUT);
  pinMode(PINO_AMARELO, OUTPUT);
  pinMode(PINO_VERMELHO, OUTPUT);
  dht.begin();
  testeDeLeds();

  conectarWifi();
  if (WiFi.status() == WL_CONNECTED) lerLimiares();
  ultimoEnvio = millis() - INTERVALO_MS;  // manda a primeira leitura já
}

void loop() {
  if (millis() - ultimoEnvio < INTERVALO_MS) return;
  ultimoEnvio = millis();

  conectarWifi();
  float temperatura = dht.readTemperature();
  float umidade = dht.readHumidity();

  // Leitura inválida nunca é enviada: um "NaN" não pode virar 0 °C no semáforo.
  if (isnan(temperatura) || isnan(umidade)) {
    apagarLeds();
    Serial.println("Falha ao ler o DHT22");
    return;
  }
  if (WiFi.status() != WL_CONNECTED) {
    apagarLeds();
    Serial.println("Sem WiFi");
    return;
  }
  enviarLeitura(temperatura, umidade);
}
