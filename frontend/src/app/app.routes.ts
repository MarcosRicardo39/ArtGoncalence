import { Routes } from '@angular/router';
import { Acesso } from './acesso';
import { Galeria, Ilustracao } from './galeria';
import { PerfilArtista } from './perfil';
import { Publicar } from './publicar';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Galeria, title: 'Galeria | ArtGoncalense' },
  { path: 'ilustracoes/:id', component: Ilustracao, title: 'Ilustração | ArtGoncalense' },
  { path: 'publicar', component: Publicar, title: 'Enviar ilustração | ArtGoncalense' },
  {
    path: 'conta/entrar',
    component: Acesso,
    data: { modo: 'entrar' },
    title: 'Entrar | ArtGoncalense',
  },
  {
    path: 'conta/cadastro',
    component: Acesso,
    data: { modo: 'cadastro' },
    title: 'Cadastro | ArtGoncalense',
  },
  {
    path: 'conta/confirmar-email',
    component: Acesso,
    data: { modo: 'confirmar' },
    title: 'Confirmar e-mail | ArtGoncalense',
  },
  {
    path: 'conta/reenviar-confirmacao',
    component: Acesso,
    data: { modo: 'reenviar' },
    title: 'Reenviar confirmação | ArtGoncalense',
  },
  {
    path: 'conta/recuperar-senha',
    component: Acesso,
    data: { modo: 'recuperar' },
    title: 'Recuperar senha | ArtGoncalense',
  },
  {
    path: 'conta/redefinir-senha',
    component: Acesso,
    data: { modo: 'redefinir' },
    title: 'Redefinir senha | ArtGoncalense',
  },
  {
    path: 'conta/perfil',
    component: PerfilArtista,
    data: { editavel: true },
    title: 'Meu perfil | ArtGoncalense',
  },
  { path: 'artistas/:id', component: PerfilArtista, title: 'Perfil de artista | ArtGoncalense' },
  { path: '**', redirectTo: '' },
];
