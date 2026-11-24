/* Versão SEM dados pessoais — é esta que vai no pacote para compartilhar.
   O app abre vazio (só com as categorias e subcategorias padrão); cada pessoa cadastra os próprios valores. */
window.__SEED = () => ({
  version: 5,
  params: { aliquota: 0.06, lag: 1, limAtencao: 0.7, limCritico: 0.9, impostoCat: 'Empresa / Profissional', impostoSub: 'Impostos / DAS' },
  categorias: [
    { nome: 'Moradia', subs: ['Aluguel','Energia (Luz)','Internet','Celular','Gás','Água','Manutenção / Pintura'] },
    { nome: 'Empresa / Profissional', subs: ['Contabilidade','Impostos / DAS','Ferramentas / Software'] },
    { nome: 'Pensão / Família', subs: ['Pensão','Dependentes'] },
    { nome: 'Transporte', subs: ['Combustível','Aplicativos','Manutenção','Passagem de ônibus'] },
    { nome: 'Alimentação', subs: [] },
    { nome: 'Saúde', subs: ['Plano de saúde','Benefício'] },
    { nome: 'Cartão de Crédito', subs: [] },
    { nome: 'Outros', subs: [] }
  ],
  receitas: [],
  despesas: [],
  ajustes: [],
  pagos: {}
});
