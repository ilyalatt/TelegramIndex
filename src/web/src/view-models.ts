import * as m from './models';

export class User {
  readonly firstName: string
  readonly lastName: string
  readonly username: string | null
  readonly userLink: string
  readonly photoLink: string | null

  constructor(model: m.User) {
    this.firstName = model.firstName;
    this.lastName = model.lastName
    this.username = model.username == null ? null : '@' + model.username;
    this.userLink = model.username == null ? `tg://user?id=${model.id}` : `https://t.me/${model.username}`;
    this.photoLink = model.photoId == null ? null : `/api/img/${model.photoId}`;
  }
}

export class Message {
  constructor(
    public readonly user: User,
    public readonly text: string,
    public readonly date: string
  ) { }
}
