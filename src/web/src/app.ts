import { autoinject } from 'aurelia-framework';
import { HttpClient } from 'aurelia-fetch-client';
import * as m from './models';
import { Message, User } from './view-models';
import { Filter } from 'filter';

@autoinject
export class App {
  private async loadData() {
    let resp = await this.httpClient.fetch('api/data', { method: 'get' });
    let json: m.Data = await resp.json();
    
    let users = new Map<number, User>();
    json.users.forEach(u => users.set(u.id, new User(u)));
    let formatDate = (dateStr: string) => {
      let date = new Date(dateStr);
      let pad2 = (n: number) => ('0' + n).slice(-2)
      let year = date.getFullYear();
      let month = pad2(date.getMonth() + 1);
      let day = pad2(date.getDate());
      let hours = pad2(date.getHours());
      let minutes = pad2(date.getMinutes());
      return `${year}-${month}-${day} ${hours}:${minutes}`;
    }
    return json.messages.map(m => new Message(users.get(m.userId)!, m.text, formatDate(m.date)));
  }

  filter?: Filter;
  private initFilter(messages: Message[]) {
    this.filter = new Filter(messages);
  }

  constructor(private readonly httpClient: HttpClient) {
    this.loadData().then(data => this.initFilter(data.reverse()));
  }
}
