namespace Dermatoglifia.Dominio.Enums;

// Os enums são gravados no banco e trafegam na API em MAIÚSCULAS_COM_SUBLINHADO
// (ex.: ArcoTendido -> "ARCO_TENDIDO"), conforme doc 05 e ADR-0003.

public enum PapelUsuario { Medico, Admin }

public enum StatusCadastro { Pendente, Ativo, Recusado, Inativo }

public enum SexoBiologico { Masculino, Feminino }

public enum SituacaoDiagnostica { Suspeita, TeaConfirmado, TeaDescartado, Controle }

public enum TipoTermo { Tcle, Tale }

public enum MetodoCaptura { LeitorOptico, TintaDigitalizada }

public enum TipoImpressao { Rolada, Plana }

public enum StatusSessao { EmAndamento, Completa, Incompleta, Cancelada }

public enum Mao { Direita, Esquerda }

public enum Dedo { Polegar, Indicador, Medio, Anelar, Minimo }

public enum StatusAnalise { Pendente, Processando, Concluida, Erro }

/// <summary>Padrão já convertido para ulnar/radial pela mão (ADR-0002).</summary>
public enum PadraoDigital { Arco, ArcoTendido, AlcaUlnar, AlcaRadial, Verticilo }

/// <summary>Classe prevista pela CNN no espaço da imagem, antes de saber a mão.</summary>
public enum ClasseImagem { Arco, ArcoTendido, AlcaEsquerda, AlcaDireita, Verticilo }

public enum OrigemDados { Sintetico, Real }
