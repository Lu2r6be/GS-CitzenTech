// PROTOTIPO VULNERAVEL: nao compilar. Veja README.md desta pasta.
namespace Prototipo;

public sealed class ExportacaoInsegura
{
    public void Exportar(Residente residente)
    {
        // VULNERAVEL 10: nome e CPF gravados em arquivo de texto, sem proteção
        File.WriteAllText("exportacao.csv", $"{residente.Nome};{residente.Cpf}");

        // VULNERAVEL 11: telefone acrescentado a um arquivo
        File.AppendAllText("contatos.txt", residente.TelefoneContato + Environment.NewLine);
    }
}
