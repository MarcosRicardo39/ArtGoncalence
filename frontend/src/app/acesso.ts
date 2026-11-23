import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ContaApi, mensagemDeErro } from './conta-api';

type Modo = 'entrar' | 'cadastro' | 'confirmar' | 'reenviar' | 'recuperar' | 'redefinir';

const TEXTOS = {
  entrar: [
    'Entre na sua conta',
    'Acesse seu perfil e prepare suas próximas ilustrações.',
    'Entrar',
  ],
  cadastro: ['Crie sua conta', 'Comece com seu nome de artista e um e-mail.', 'Criar minha conta'],
  confirmar: [
    'Confirme seu e-mail',
    'Informe o código que recebeu para habilitar publicações.',
    'Confirmar e-mail',
  ],
  reenviar: [
    'Reenvie a confirmação',
    'Informe o e-mail usado no cadastro.',
    'Solicitar novo código',
  ],
  recuperar: ['Recupere sua senha', 'Informe o e-mail associado à sua conta.', 'Solicitar código'],
  redefinir: [
    'Defina uma nova senha',
    'Use o código de recuperação enviado por e-mail.',
    'Salvar nova senha',
  ],
} satisfies Record<Modo, readonly [string, string, string]>;

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-acesso',
  templateUrl: './acesso.html',
})
export class Acesso {
  private readonly api = inject(ContaApi);
  private readonly router = inject(Router);
  readonly modo = inject(ActivatedRoute).snapshot.data['modo'] as Modo;
  readonly textos = TEXTOS[this.modo];

  readonly pedeNome = this.modo === 'cadastro';
  readonly pedeSenha =
    this.modo === 'entrar' || this.modo === 'cadastro' || this.modo === 'redefinir';
  readonly pedeCodigo = this.modo === 'confirmar' || this.modo === 'redefinir';
  readonly sessaoAtiva = () => this.api.sessao() !== null;
  readonly operando = signal(false);
  readonly senhaVisivel = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');

  readonly formulario = new FormGroup({
    email: new FormControl(this.api.emailInformado, {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(256)],
    }),
    nomePublico: new FormControl('', {
      nonNullable: true,
      validators: this.pedeNome
        ? [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)]
        : [],
    }),
    senha: new FormControl('', {
      nonNullable: true,
      validators: this.pedeSenha
        ? [
            Validators.required,
            Validators.maxLength(128),
            ...(this.modo === 'entrar' ? [] : [Validators.minLength(15)]),
          ]
        : [],
    }),
    codigo: new FormControl('', {
      nonNullable: true,
      validators: this.pedeCodigo
        ? [Validators.required, Validators.maxLength(4096), Validators.pattern(/\S/)]
        : [],
    }),
  });

  async enviar(): Promise<void> {
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid || this.operando()) return;

    this.erro.set('');
    this.aviso.set('');
    this.operando.set(true);
    const { email, senha, codigo, nomePublico } = this.formulario.getRawValue();
    this.api.emailInformado = email.trim();
    const endereco = this.api.emailInformado;
    try {
      switch (this.modo) {
        case 'entrar':
          await this.api.autenticar(endereco, senha);
          await this.router.navigateByUrl('/conta/perfil');
          break;
        case 'cadastro':
          await this.api.enviar('registrar', {
            email: endereco,
            senha,
            nomePublico: nomePublico.trim(),
          });
          this.formulario.controls.senha.reset();
          this.aviso.set('Se aplicável, enviaremos instruções de confirmação para esse e-mail.');
          break;
        case 'confirmar':
          await this.api.enviar('confirmar-email', { email: endereco, codigo: codigo.trim() });
          await this.api.atualizarSessao();
          this.formulario.controls.codigo.reset();
          this.aviso.set('E-mail confirmado.');
          break;
        case 'reenviar':
          await this.api.enviar('reenviar-confirmacao', { email: endereco });
          this.aviso.set('Se aplicável, enviaremos um novo código de confirmação.');
          break;
        case 'recuperar':
          await this.api.enviar('solicitar-recuperacao', { email: endereco });
          this.aviso.set('Se aplicável, enviaremos instruções para redefinir sua senha.');
          break;
        case 'redefinir':
          await this.api.enviar('redefinir-senha', {
            email: endereco,
            codigo: codigo.trim(),
            senha,
          });
          await this.api.atualizarSessao();
          this.formulario.controls.senha.reset();
          this.formulario.controls.codigo.reset();
          this.aviso.set('Senha redefinida. Entre com a nova senha.');
          break;
      }
    } catch (error) {
      const padrao =
        this.modo === 'entrar'
          ? 'Não foi possível entrar. Confira seu e-mail e sua senha.'
          : this.pedeCodigo
            ? 'Não foi possível concluir. Confira o código ou solicite um novo.'
            : 'Não foi possível concluir a solicitação. Tente novamente.';
      this.erro.set(mensagemDeErro(error, padrao));
    } finally {
      this.operando.set(false);
    }
  }
}
