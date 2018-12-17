export interface Message {
  userId: number
  date: string
  text: string
}

export interface User {
  id: number
  firstName: string
  lastName: string
  username?: string
}

export interface Data {
  messages: Message[]
  users: User[]
}
