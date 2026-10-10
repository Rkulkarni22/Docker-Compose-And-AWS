import apiClient from "./client";

export interface User {
  id: number;
  name: string;
  email: string;
  age: number | null;
}

export const getUsers = async (): Promise<User[]> => {
  const response = await apiClient.get<User[]>("/api/Home"); // match your controller's route
  return response.data;
};